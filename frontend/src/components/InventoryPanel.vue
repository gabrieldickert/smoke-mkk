<script setup lang="ts">
import { AnimatePresence, motion, useReducedMotion } from 'motion-v'
import { computed, nextTick, onUnmounted, ref, watch } from 'vue'
import {
  fetchInventory,
  fetchSearch,
  placeName,
  type Category,
  type Inventory,
  type Location,
  type SearchHit,
} from '../api'

// docs/PLAN.md §4.3 F10 / F15: list of locations while none is selected; once one is, its header,
// the machine picker (2+ machines) and the picked machine's stock. This list is the text fallback
// for the map. Ids in `select`, `distances` and `nearestId` are location ids; only the inventory
// fetch takes a machine id.
const props = defineProps<{
  locations: Location[]
  status: 'loading' | 'ready' | 'error'
  location: Location | null
  distances: Map<number, number> | null // km per location id, once the visitor's position is known
  nearestId: number | null
  geoState: 'idle' | 'locating' | 'ok' | 'failed' | 'denied' // the visitor's position request (HomeView)
}>()
// locate: the geo.retry button (F27); HomeView runs the CTA's locate(true), without its scroll.
const emit = defineEmits<{ select: [id: number | null]; locate: [] }>()

// docs/PLAN.md §6: verfügbar (qty > 3) · fast weg (1–3) · ausverkauft (0)
const LOW_STOCK_MAX = 3

// §6 labels; key order = §5 display order (age-restricted goods first, `Other` last: products no
// keyword matched, B7).
const CATEGORY_LABEL: Record<Category, string> = {
  Vape: 'Vapes',
  Tobacco: 'Tabak',
  Accessory: 'Rauchzubehör',
  Drink: 'Drinks',
  Snack: 'Snacks',
  Other: 'Sonstiges',
}

const reduceMotion = useReducedMotion()
const state = ref<'idle' | 'loading' | 'error' | 'ready'>('idle')
const inventory = ref<Inventory | null>(null)
let controller: AbortController | null = null

// Product search hits (§4.3 F31): machine id → matching in-stock product names, from the last
// completed `GET /api/search`; empty while the query is shorter than 2 characters. Declared before
// the machine watcher below, which reads it synchronously (immediate).
const hits = ref<SearchHit[]>([])
const hitMachines = computed(() => new Map(hits.value.map((h) => [h.machineId, h.products])))

// The picked machine (§4.3 F15): a new location preselects its first machine (API order, by label);
// with product hits (F31) the first of its machines that has one.
const machineId = ref<number | null>(null)
watch(
  () => props.location?.id,
  () => {
    const machines = props.location?.machines ?? []
    machineId.value = (machines.find((m) => hitMachines.value.has(m.id)) ?? machines[0])?.id ?? null
  },
  { immediate: true },
)
// §6 panel.machineFallback: "Automat {n}" (1-based) when a machine has no label.
const machineLabel = (label: string, index: number) => label || `Automat ${index + 1}`

// Photo of the picked machine (F28): §6 panel.photoAlt — "Foto vom Automaten {label} in {place}",
// without the label part when the machine has none (never the machineFallback text).
// `photoFailed` hides the <img> for good after an `error`; it resets with every machine change.
const photoFailed = ref(false)
const photo = computed(() => {
  const m = props.location?.machines.find((m) => m.id === machineId.value)
  if (!m?.pictureUrl || !props.location || photoFailed.value) return null
  const label = m.label ? ` ${m.label}` : ''
  return { url: m.pictureUrl, alt: `Foto vom Automaten${label} in ${placeName(props.location)}` }
})

watch(
  machineId,
  async (id) => {
    controller?.abort()
    inventory.value = null
    photoFailed.value = false
    if (id == null) {
      state.value = 'idle'
      return
    }
    state.value = 'loading'
    const c = (controller = new AbortController())
    try {
      inventory.value = await fetchInventory(id, c.signal)
      state.value = 'ready'
    } catch {
      if (!c.signal.aborted) state.value = 'error'
    }
  },
  { immediate: true },
)

onUnmounted(() => {
  controller?.abort()
  clearTimeout(searchTimer)
  searchController?.abort()
})

// Within a category, in-stock items first. Array#sort is stable, so the API's name order stays.
const groups = computed(() =>
  (Object.keys(CATEGORY_LABEL) as Category[])
    .map((category) => ({
      category,
      label: CATEGORY_LABEL[category],
      items: (inventory.value?.items.filter((i) => i.category === category) ?? []).sort(
        (a, b) => Number(b.quantity > 0) - Number(a.quantity > 0),
      ),
    }))
    .filter((g) => g.items.length),
)

// §6 panel.summary (F30: product count only): n = items with quantity > 0; singular 1 Produkt.
// The same string feeds the visible line and the status line (one atomic contextual message,
// ui-ux-pro-max "Contextual Live Badge Updates").
const summary = computed(() => {
  const products = (inventory.value?.items ?? []).filter((i) => i.quantity > 0).length
  return `${products} ${products === 1 ? 'Produkt' : 'Produkte'} im Automaten`
})

// §6 panel.updatedAt, no seconds.
const updatedAt = computed(() =>
  inventory.value?.updatedAt
    ? new Date(inventory.value.updatedAt).toLocaleString('de-DE', {
        dateStyle: 'medium',
        timeStyle: 'short',
      })
    : null,
)

// Search (docs/PLAN.md §4.3 F10): postal-code prefix, or substring of display name, city or street;
// case- and accent-insensitive, so "schluchtern" finds Schlüchtern. Filters the list only, never
// the map. The query lives here, so it survives the detail view and "Alle Automaten".
const query = ref('')
const fold = (s: string) => s.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase().trim()
const placeMatch = (l: Location, q: string) =>
  fold(l.postalCode).startsWith(q) ||
  [placeName(l), l.city, l.street].some((field) => fold(field).includes(q))

// Product search (§4.3 F31): from 2 folded characters on, `GET /api/search?q=` debounced 250 ms,
// the previous request aborted (ui-ux-pro-max "Autocomplete": results while typing, one request
// per pause). A failed or aborted request is ignored — the place matches still show, no error
// text; the last hits stay until the next response, so the list does not flash while typing.
let searchTimer: ReturnType<typeof setTimeout> | undefined
let searchController: AbortController | null = null
watch(query, (q) => {
  clearTimeout(searchTimer)
  searchController?.abort()
  if (fold(q).length < 2) {
    hits.value = []
    return
  }
  searchTimer = setTimeout(async () => {
    const c = (searchController = new AbortController())
    try {
      hits.value = await fetchSearch(q.trim(), c.signal)
    } catch {
      /* ignored: place matches still show */
    }
  }, 250)
})

// Union of place matches and locations with a machine in the hits, in the usual order. With a
// known position the list is sorted by distance (nearest first), otherwise API order (name).
const filtered = computed(() => {
  const q = fold(query.value)
  const matches = q
    ? props.locations.filter(
        (l) => placeMatch(l, q) || l.machines.some((m) => hitMachines.value.has(m.id)),
      )
    : props.locations
  const d = props.distances
  return d ? [...matches].sort((a, b) => (d.get(a.id) ?? 0) - (d.get(b.id) ?? 0)) : matches
})

// The names line of a row that is in the list only because of a product hit: all hit names over
// the location's machines, deduplicated, joined with " · " (§4.3 F31). "" for place matches.
const productsLine = (l: Location) => {
  const q = fold(query.value)
  if (!q || placeMatch(l, q)) return ''
  return [...new Set(l.machines.flatMap((m) => hitMachines.value.get(m.id) ?? []))].join(' · ')
}

// §6 geo.distance: one decimal, de-DE ("3,2 km"), straight line.
const distance = (id: number) => {
  const km = props.distances?.get(id)
  return km == null
    ? null
    : `${km.toLocaleString('de-DE', { minimumFractionDigits: 1, maximumFractionDigits: 1 })} km`
}

// §6 search.count (list rows = locations) while a query is set. Shown under the search field only
// with ≥ 1 match (0 shows search.noResults instead); the status line announces it either way.
const count = computed(() => {
  if (!fold(query.value)) return ''
  const n = filtered.value.length
  return n === 1 ? '1 Standort' : `${n} Standorte`
})
// §6 geo.locating / geo.unavailable (F24) / geo.blocked (F27): nothing when idle or once a
// position is known.
const GEO_LINE = {
  idle: '',
  ok: '',
  locating: 'Standort wird ermittelt …',
  failed: 'Kein Standort freigegeben. Such per PLZ oder Ort.',
  denied: 'Standort ist im Browser blockiert. Erlaub ihn über das Symbol links neben der Webadresse.',
} as const
const geoLine = computed(() => GEO_LINE[props.geoState])

// §6 geo.retry (F27) after geo.unavailable / geo.blocked. It stays mounted through any new attempt
// that follows (its own, or HomeView's after a re-allow), so keyboard focus survives the
// locating → failed/denied round trip (a denied site fails within milliseconds) and is still on it
// when a success replaces the list (see the location watcher below). Its own success opens the
// nearest location and focus moves to its heading, as for a pick from the list.
let retrying = false
const reattempt = ref(false) // 'locating' right after 'failed' / 'denied'
const showRetry = computed(
  () =>
    props.geoState === 'failed' ||
    props.geoState === 'denied' ||
    (reattempt.value && props.geoState === 'locating'),
)
watch(
  () => props.geoState,
  (s, old) => {
    reattempt.value = s === 'locating' && (old === 'failed' || old === 'denied')
    if (s === 'locating' || !retrying) return
    retrying = false
    if (s !== 'ok') focusHeadingNext = false
  },
)
function retry() {
  if (props.geoState === 'locating') return
  retrying = true
  focusHeadingNext = true
  emit('locate')
}

// One persistent status line instead of aria-live around the whole list (ui-ux-pro-max
// "Contextual Live Updates": one atomic message, not a competing live region).
const announcement = computed(() => {
  if (!props.location) return [count.value, geoLine.value].filter(Boolean).join(' · ')
  if (state.value === 'error') return "Der Bestand lädt gerade nicht. Versuch's gleich noch mal."
  if (state.value !== 'ready' || !inventory.value) return ''
  if (!inventory.value.items.length) return 'Für diesen Automaten ist noch kein Bestand hinterlegt.'
  return `${placeName(props.location)} · ${summary.value}`
})

const address = (l: Location) =>
  [l.street, `${l.postalCode} ${l.city}`].filter(Boolean).join(', ')

// Focus management: the list button vanishes when the detail replaces it, so a selection made in
// the panel moves focus to the detail heading, and "Alle Automaten" returns it to the button of
// the location just left. AnimatePresence (mode="wait") mounts the new view only after the old one
// has faded out, so focus is triggered from the element refs (a nextTick after the emit would run
// while the old view is still leaving). Vue calls a function ref while the new subtree is still
// detached from the document, hence the nextTick inside it. Map selections set neither flag and
// leave focus on the marker.
let focusHeadingNext = false
let focusButtonId: number | null = null
let focusSearchNext = false // the location left is hidden by the query (it was picked on the map)

function choose(id: number) {
  focusHeadingNext = true
  emit('select', id)
}

function back() {
  const id = props.location?.id
  if (filtered.value.some((l) => l.id === id)) focusButtonId = id ?? null
  else focusSearchNext = true
  emit('select', null)
}

// A selection made without the panel (HomeView locating by itself after the visitor re-allowed
// the location, F27) replaces the list under the focus: if focus was inside the list view (e.g. on
// the retry button), it moves to the detail heading like a list pick. Focus elsewhere (a map pin,
// the section) stays. Pre-flush watcher: the list is still in the DOM here.
const panel = ref<HTMLElement | null>(null)
watch(
  () => props.location,
  (now, before) => {
    if (now && !before && panel.value?.contains(document.activeElement)) focusHeadingNext = true
  },
)

function headingRef(el: unknown) {
  if (!focusHeadingNext || !(el instanceof HTMLElement)) return
  focusHeadingNext = false
  nextTick(() => el.focus({ preventScroll: true }))
}

function searchRef(el: unknown) {
  if (!focusSearchNext || !(el instanceof HTMLElement)) return
  focusSearchNext = false
  nextTick(() => el.focus({ preventScroll: true }))
}

function buttonRef(el: unknown, id: number) {
  if (focusButtonId !== id || !(el instanceof HTMLElement)) return
  focusButtonId = null
  nextTick(() => el.focus({ preventScroll: true }))
}

function stock(quantity: number) {
  if (quantity === 0)
    return { label: 'ausverkauft', text: 'text-destructive-foreground', dot: 'bg-destructive' }
  if (quantity <= LOW_STOCK_MAX) return { label: 'fast weg', text: 'text-warning', dot: 'bg-warning' }
  return { label: 'verfügbar', text: 'text-success', dot: 'bg-success' }
}

const price = (cents: number) =>
  (cents / 100).toLocaleString('de-DE', { style: 'currency', currency: 'EUR' })

// §6 geo.nearest badge; white on primary 5.7:1.
const badge =
  'inline-flex shrink-0 items-center rounded-full bg-primary px-2 py-0.5 text-xs font-semibold text-primary-foreground'
const muted = 'inline-flex shrink-0 items-center rounded-full border border-border px-2 py-0.5 text-xs font-medium text-muted-foreground'
// F16/F17: md:pr-3 keeps prices ~12 px off the desktop scrollbar (phones have overlay scrollbars
// and keep symmetric sides); a stable gutter stops the content jumping when the list starts or
// stops overflowing (ui-ux-pro-max "Content Jumping"). No top padding here: the stock list starts
// with its sticky heading flush at the edge (F17/F18); the location list adds its own pt-2.
const scrollBox =
  'mt-3 min-h-0 flex-1 overflow-y-auto overscroll-contain border-t border-border/60 md:pr-3 [scrollbar-gutter:stable]'
</script>

<template>
  <div
    ref="panel"
    class="relative flex h-full flex-col overflow-hidden rounded-xl border border-border bg-card p-5 text-card-foreground"
  >
    <p role="status" class="sr-only">{{ announcement }}</p>
    <!-- List ↔ detail and location ↔ location cross-fade: arrive decelerating, leave accelerating
         (ui-ux-pro-max). Machine ↔ machine at one location cross-fades only the stock below. -->
    <AnimatePresence mode="wait">
      <motion.div
        :key="location?.id ?? 'list'"
        :initial="reduceMotion ? false : { opacity: 0 }"
        :animate="{ opacity: 1, transition: { duration: reduceMotion ? 0 : 0.2, ease: 'easeOut' } }"
        :exit="{ opacity: 0, transition: { duration: reduceMotion ? 0 : 0.15, ease: 'easeIn' } }"
        class="flex min-h-0 flex-1 flex-col"
      >
        <template v-if="location">
          <button
            id="panel-back"
            type="button"
            class="-mt-2 -ml-2 inline-flex min-h-11 w-fit cursor-pointer items-center gap-1 rounded-lg px-2 text-sm font-semibold text-muted-foreground transition-colors duration-200 hover:bg-muted hover:text-foreground"
            @click="back"
          >
            <svg
              class="size-4 shrink-0"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <path d="m15 18-6-6 6-6" />
            </svg>
            Alle Automaten
          </button>
          <div class="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1">
            <h3 id="panel-location" :ref="headingRef" tabindex="-1" class="rounded-md text-2xl font-bold">
              {{ placeName(location) }}
            </h3>
            <span v-if="location.id === nearestId" :class="badge">Am nächsten</span>
          </div>
          <div class="flex flex-wrap items-center justify-between gap-x-4">
            <address class="py-1 text-sm not-italic text-muted-foreground">
              {{ address(location) }}
            </address>
            <a
              v-if="location.googleMapsUrl"
              :href="location.googleMapsUrl"
              target="_blank"
              rel="noopener"
              aria-describedby="panel-location"
              class="-mr-1 inline-flex min-h-11 items-center gap-1.5 rounded-lg px-1 font-semibold text-secondary transition-colors duration-200 hover:text-foreground"
            >
              Route
              <svg
                class="size-4"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
                aria-hidden="true"
              >
                <path d="M7 17 17 7M8 7h9v9" />
              </svg>
            </a>
          </div>

          <!-- Machine picker (§4.3 F15), only at 2+ machines: native radios, so Tab enters the group
               at the checked machine and the arrow keys switch (and load) machines. Pills per the
               ui-ux-pro-max compact-control rules: ≥ 44 px, 8 px gaps, one line each, selected state
               as a filled pill (fill + contrast, not hue alone), the site's focus ring on the pill. -->
          <fieldset v-if="location.machines.length > 1" class="mt-2">
            <legend class="text-sm font-semibold">Welcher Automat?</legend>
            <div class="mt-1.5 flex flex-wrap gap-2">
              <label
                v-for="(m, i) in location.machines"
                :key="m.id"
                class="inline-flex min-h-11 min-w-11 cursor-pointer items-center justify-center rounded-full border border-border px-4 text-sm font-semibold whitespace-nowrap text-foreground transition-colors duration-200 hover:bg-muted has-checked:border-primary has-checked:bg-primary has-checked:text-primary-foreground has-focus-visible:outline-2 has-focus-visible:outline-offset-2 has-focus-visible:outline-ring"
              >
                <input v-model="machineId" type="radio" name="machine" :value="m.id" class="sr-only" />
                {{ machineLabel(m.label, i) }}
              </label>
            </div>
          </fieldset>

          <!-- Stock of the picked machine; cross-fades when another machine is picked (keyed by
               machine id). No initial fade: the location view around it already fades in. -->
          <AnimatePresence mode="wait" :initial="false">
            <motion.div
              :key="machineId ?? 'none'"
              :initial="reduceMotion ? false : { opacity: 0 }"
              :animate="{ opacity: 1, transition: { duration: reduceMotion ? 0 : 0.2, ease: 'easeOut' } }"
              :exit="{ opacity: 0, transition: { duration: reduceMotion ? 0 : 0.15, ease: 'easeIn' } }"
              class="flex min-h-0 flex-1 flex-col"
            >
              <!-- Machine photo (F28): the box is reserved from the intrinsic 3:4 ratio (width/height
                   attributes + aspect-ratio), so nothing jumps when it loads; left-aligned like the
                   text column above it; gone for good after an error (ui-ux-pro-max: reserve media
                   space, descriptive alt, lazy load). -->
              <img
                v-if="photo"
                :src="photo.url"
                :alt="photo.alt"
                loading="lazy"
                decoding="async"
                width="1500"
                height="2000"
                class="mt-3 aspect-[3/4] h-48 w-auto shrink-0 self-start rounded-lg border border-border/60 bg-muted object-cover"
                @error="photoFailed = true"
              />
              <div
                v-if="state === 'ready' && inventory?.items.length"
                :class="['text-sm text-muted-foreground tabular-nums', (location.machines.length > 1 || photo) && 'mt-3']"
              >
                <p>{{ summary }}</p>
                <p v-if="updatedAt">Stand: {{ updatedAt }}</p>
              </div>

              <div :class="scrollBox" :aria-busy="state === 'loading'">
                <p v-if="state === 'error'" class="pt-2 text-destructive-foreground">
                  Der Bestand lädt gerade nicht. Versuch's gleich noch mal.
                </p>
                <ul v-else-if="state === 'loading'" class="space-y-2 pt-2" aria-hidden="true">
                  <li v-for="n in 6" :key="n" class="h-12 animate-pulse rounded-lg bg-muted" />
                </ul>
                <template v-else-if="inventory">
                  <p v-if="!inventory.items.length" class="pt-2 text-muted-foreground">
                    Für diesen Automaten ist noch kein Bestand hinterlegt.
                  </p>
                  <section v-for="group in groups" :key="group.category" class="mb-5 last:mb-0">
                    <!-- Sticky inside the panel's scroll box, so five groups stay orientable (ui-ux-pro-max).
                         F17: it must hide everything scrolling under it. The box has no top padding
                         (sticky insets are measured from the padding edge), so top-0 sticks flush at
                         the box's top; md:-mr-3 md:pr-3 bleeds its background over the F16 right gap. The
                         hairline stays on unstuck too: a plain group rule, the same line as the rows'
                         dividers, and no browser-specific "stuck" query. -->
                    <h4
                      class="sticky top-0 z-10 mb-1 border-b border-border/60 bg-card py-1.5 text-xs font-semibold uppercase tracking-[0.2em] text-muted-foreground md:-mr-3 md:pr-3"
                    >
                      {{ group.label }}
                    </h4>
                    <ul class="divide-y divide-border/60">
                      <motion.li
                        v-for="(item, index) in group.items"
                        :key="item.productId"
                        :initial="reduceMotion ? false : { opacity: 0, y: 8 }"
                        :animate="{ opacity: 1, y: 0 }"
                        :transition="{ duration: 0.25, delay: reduceMotion ? 0 : index * 0.04 }"
                        :class="[
                          'flex items-start gap-3 py-1.5',
                          item.quantity === 0 && 'text-muted-foreground',
                        ]"
                      >
                        <!-- Text row (B7/F29, no product images): the name column shrinks (min-w-0) and
                             wraps, the price is shrink-0 + nowrap, so a long name never pushes it off
                             its line (ui-ux-pro-max "Compact label layout"). The stock badge and the
                             quantity are each one unbreakable unit ("Compact Label Overflow"). Sold out
                             = the whole row muted, no strike-through; muted-foreground on card 6.4:1. -->
                        <span class="min-w-0 flex-1">
                          <span class="block font-medium leading-snug break-words">{{ item.name }}</span>
                          <span class="mt-0.5 flex flex-wrap items-center gap-x-2 text-sm leading-snug">
                            <span
                              :class="[
                                'inline-flex items-center gap-1.5 whitespace-nowrap',
                                stock(item.quantity).text,
                              ]"
                            >
                              <span
                                :class="['size-2 rounded-full', stock(item.quantity).dot]"
                                aria-hidden="true"
                              />
                              {{ stock(item.quantity).label }}
                            </span>
                            <span class="whitespace-nowrap text-muted-foreground tabular-nums"
                              >{{ item.quantity }} Stück</span
                            >
                          </span>
                        </span>
                        <span class="shrink-0 font-semibold leading-snug whitespace-nowrap tabular-nums">{{
                          price(item.priceCents)
                        }}</span>
                      </motion.li>
                    </ul>
                  </section>
                </template>
              </div>
            </motion.div>
          </AnimatePresence>
        </template>

        <template v-else>
          <!-- Same small uppercase label the old panel title had: the section's h2 sits right above,
               so the panel heading stays quiet; in the detail view the location name is the h3.
               §6 panel.title: the fixed total of locations, not the search matches (F26). -->
          <h3 class="text-sm font-semibold uppercase tracking-[0.2em] text-secondary">
            {{ status === 'ready' ? `Alle ${locations.length} Standorte` : 'Alle Standorte' }}
          </h3>
          <template v-if="status !== 'error'">
            <p class="mt-2 text-muted-foreground">
              Such dir einen Automaten aus. Hier steht dann, was drin ist.
            </p>
            <label for="machine-search" class="mt-3 block text-sm font-semibold">
              PLZ, Ort oder Produkt
            </label>
            <!-- text-base: 16 px keeps iOS from zooming into the field. -->
            <input
              id="machine-search"
              :ref="searchRef"
              v-model="query"
              type="search"
              placeholder="z. B. 63607, Wächtersbach oder Elfbar"
              autocomplete="off"
              enterkeyhint="search"
              class="mt-1 min-h-11 w-full rounded-lg border border-input bg-background px-3 text-base text-foreground placeholder:text-muted-foreground"
            />
            <!-- One small muted block under the field (F24): the visible search.count, then the
                 location feedback, each only when it applies; the list simply starts below it.
                 muted-foreground on card 6.4:1. -->
            <p
              v-if="(count && filtered.length) || geoLine"
              class="mt-1.5 text-sm text-muted-foreground tabular-nums"
            >
              <span v-if="count && filtered.length" class="block">{{ count }}</span>
              <!-- The retry button's 44 px hit area comes from min-h-11; -my-3 takes it back out of
                   the 20 px line, so the line stays compact. secondary on card 6.1:1. -->
              <span v-if="geoLine" class="block"
                >{{ geoLine }}<template v-if="showRetry"
                  >{{ ' ' }}<button
                    type="button"
                    class="-my-3 inline-flex min-h-11 cursor-pointer items-center rounded-md px-1 font-semibold text-secondary underline-offset-2 hover:underline"
                    @click="retry"
                  >
                    Nochmal versuchen
                  </button></template
                ></span
              >
            </p>
          </template>
          <div :class="[scrollBox, 'pt-2']" :aria-busy="status === 'loading'">
            <!-- The map gets no overlay; its load error is reported here, next to the list. -->
            <p v-if="status === 'error'" role="alert" class="text-destructive-foreground">
              Die Standorte konnten nicht geladen werden.
            </p>
            <ul v-else-if="status === 'loading'" class="space-y-1" aria-hidden="true">
              <li v-for="n in 6" :key="n" class="h-14 animate-pulse rounded-lg bg-muted" />
            </ul>
            <p v-else-if="!filtered.length" class="px-3 py-2 text-muted-foreground">
              Da steht noch keiner. Schau auf der Karte, welcher Automat am nächsten ist.
            </p>
            <ul v-else class="space-y-1">
              <li v-for="l in filtered" :key="l.id">
                <button
                  :ref="(el) => buttonRef(el, l.id)"
                  type="button"
                  class="flex min-h-11 w-full cursor-pointer items-center gap-3 rounded-lg px-3 py-2 text-left transition-colors duration-200 hover:bg-muted focus-visible:-outline-offset-2"
                  @click="choose(l.id)"
                >
                  <span class="min-w-0 flex-1">
                    <span class="flex flex-wrap items-center gap-x-2 gap-y-0.5">
                      <span class="font-semibold">{{ placeName(l) }}</span>
                      <!-- §6 location.machineCount, only at 2+; outlined, so it reads as a fact and
                           the filled "Am nächsten" badge stays the one that stands out. -->
                      <span v-if="l.machines.length > 1" :class="muted"
                        >{{ l.machines.length }} Automaten</span
                      >
                      <span v-if="l.id === nearestId" :class="badge">Am nächsten</span>
                    </span>
                    <span class="block text-sm text-muted-foreground">{{ address(l) }}</span>
                    <!-- F31: the matching products of a row that is here only because of a product
                         hit; one line, ellipsis, the full list in `title` (ui-ux-pro-max "Truncation":
                         ellipsis plus a way to the full text). muted-foreground on card 6.4:1.
                         contain-inline-size: the nowrap text must not count towards the panel's
                         min-content width, or the phone grid column grows past the viewport. -->
                    <span
                      v-if="productsLine(l)"
                      class="block truncate text-sm text-muted-foreground contain-inline-size"
                      :title="productsLine(l)"
                      >{{ productsLine(l) }}</span
                    >
                  </span>
                  <span
                    v-if="distance(l.id)"
                    class="shrink-0 text-sm whitespace-nowrap text-muted-foreground tabular-nums"
                    >{{ distance(l.id) }}</span
                  >
                  <svg
                    class="size-4 shrink-0 text-muted-foreground"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    stroke-width="2"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    aria-hidden="true"
                  >
                    <path d="m9 18 6-6-6-6" />
                  </svg>
                </button>
              </li>
            </ul>
          </div>
        </template>
      </motion.div>
    </AnimatePresence>
  </div>
</template>
