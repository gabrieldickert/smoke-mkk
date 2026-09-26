<script setup lang="ts">
import { AnimatePresence, motion, useReducedMotion } from 'motion-v'
import { computed, nextTick, onUnmounted, ref, watch } from 'vue'
import { fetchInventory, placeName, type Category, type Inventory, type Location } from '../api'

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
}>()
const emit = defineEmits<{ select: [id: number | null] }>()

// docs/PLAN.md §6: verfügbar (qty > 3) · fast weg (1–3) · ausverkauft (0)
const LOW_STOCK_MAX = 3

// §6 labels; key order = §5 display order (age-restricted goods first).
const CATEGORY_LABEL: Record<Category, string> = {
  Vape: 'Vapes',
  Tobacco: 'Tabak',
  Accessory: 'Rauchzubehör',
  Drink: 'Drinks',
  Snack: 'Snacks',
}

const reduceMotion = useReducedMotion()
const state = ref<'idle' | 'loading' | 'error' | 'ready'>('idle')
const inventory = ref<Inventory | null>(null)
// Product ids whose image failed to load; they fall back to the category placeholder.
const brokenImages = ref(new Set<number>())
let controller: AbortController | null = null

// The picked machine (§4.3 F15): a new location preselects its first machine (API order, by label).
const machineId = ref<number | null>(null)
watch(
  () => props.location?.id,
  () => (machineId.value = props.location?.machines[0]?.id ?? null),
  { immediate: true },
)
// §6 panel.machineFallback: "Automat {n}" (1-based) when a machine has no label.
const machineLabel = (label: string, index: number) => label || `Automat ${index + 1}`

watch(
  machineId,
  async (id) => {
    controller?.abort()
    inventory.value = null
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

onUnmounted(() => controller?.abort())

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

// §6 panel.summary: n = items with quantity > 0, m = sum of quantities; singular 1 Produkt / 1 Artikel
// ("Artikel" is the same word in singular and plural)
const summary = computed(() => {
  const items = inventory.value?.items ?? []
  const products = items.filter((i) => i.quantity > 0).length
  const units = items.reduce((sum, i) => sum + i.quantity, 0)
  return `${products} ${products === 1 ? 'Produkt' : 'Produkte'} · ${units} Artikel im Automaten`
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
// With a known position the list is sorted by distance (nearest first), otherwise API order (name).
const filtered = computed(() => {
  const q = fold(query.value)
  const hits = q
    ? props.locations.filter(
        (l) =>
          fold(l.postalCode).startsWith(q) ||
          [placeName(l), l.city, l.street].some((field) => fold(field).includes(q)),
      )
    : props.locations
  const d = props.distances
  return d ? [...hits].sort((a, b) => (d.get(a.id) ?? 0) - (d.get(b.id) ?? 0)) : hits
})

// §6 geo.distance: one decimal, de-DE ("3,2 km"), straight line.
const distance = (id: number) => {
  const km = props.distances?.get(id)
  return km == null
    ? null
    : `${km.toLocaleString('de-DE', { minimumFractionDigits: 1, maximumFractionDigits: 1 })} km`
}

// One persistent status line instead of aria-live around the whole list (ui-ux-pro-max
// "Contextual Live Updates": one atomic message, not a competing live region).
const announcement = computed(() => {
  if (!props.location) {
    if (!fold(query.value)) return ''
    const n = filtered.value.length
    return n === 1 ? '1 Standort' : `${n} Standorte` // §6 search.count: list rows = locations
  }
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
              <div
                v-if="state === 'ready' && inventory?.items.length"
                :class="['text-sm text-muted-foreground tabular-nums', location.machines.length > 1 && 'mt-3']"
              >
                <p>{{ summary }}</p>
                <p v-if="updatedAt">Stand: {{ updatedAt }}</p>
              </div>

              <div :class="scrollBox" :aria-busy="state === 'loading'">
                <p v-if="state === 'error'" class="pt-2 text-destructive-foreground">
                  Der Bestand lädt gerade nicht. Versuch's gleich noch mal.
                </p>
                <ul v-else-if="state === 'loading'" class="space-y-3 pt-2" aria-hidden="true">
                  <li v-for="n in 5" :key="n" class="h-14 animate-pulse rounded-lg bg-muted" />
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
                        class="flex items-center gap-3 py-2.5"
                      >
                        <!-- Fixed square box: the image never shifts the row (ui-ux-pro-max "Content Jumping"). -->
                        <span
                          :class="[
                            'grid size-14 shrink-0 place-items-center overflow-hidden rounded-lg bg-muted p-1',
                            item.quantity === 0 && 'grayscale opacity-60',
                          ]"
                        >
                          <img
                            v-if="item.imageUrl && !brokenImages.has(item.productId)"
                            :src="item.imageUrl"
                            :alt="item.name"
                            loading="lazy"
                            width="48"
                            height="48"
                            class="size-12 object-contain"
                            @error="brokenImages.add(item.productId)"
                          />
                          <svg
                            v-else
                            class="size-8 text-secondary"
                            viewBox="0 0 24 24"
                            fill="none"
                            stroke="currentColor"
                            stroke-width="1.6"
                            stroke-linecap="round"
                            stroke-linejoin="round"
                            aria-hidden="true"
                          >
                            <!-- Drink: can -->
                            <template v-if="item.category === 'Drink'">
                              <path d="M8 5h8l1 2v12a2 2 0 0 1-2 2H9a2 2 0 0 1-2-2V7z" />
                              <path d="M7 9h10M7 17h10M10.5 3h3" />
                            </template>
                            <!-- Vape: disposable device -->
                            <template v-else-if="item.category === 'Vape'">
                              <rect x="8" y="7" width="8" height="15" rx="2.5" />
                              <path d="M10 7V4.5a1.5 1.5 0 0 1 1.5-1.5h1A1.5 1.5 0 0 1 14 4.5V7" />
                              <path d="M11 18h2" />
                            </template>
                            <!-- Tobacco: cigarette pack -->
                            <template v-else-if="item.category === 'Tobacco'">
                              <path d="M6 9h12v11.5a1.5 1.5 0 0 1-1.5 1.5h-9A1.5 1.5 0 0 1 6 20.5z" />
                              <path d="M6 13h12M9 9V4.5h2V9M13 9V3.5h2V9" />
                            </template>
                            <!-- Accessory: lighter -->
                            <template v-else-if="item.category === 'Accessory'">
                              <rect x="8" y="11" width="8" height="11" rx="1.5" />
                              <path d="M8 11V9h5v2" />
                              <circle cx="15" cy="9" r="1.6" />
                              <path d="M10.5 1.5c-1.6 1.7-1.6 3.4 0 4.6 1.6-1.2 1.6-2.9 0-4.6z" />
                            </template>
                            <!-- Snack: bag -->
                            <template v-else-if="item.category === 'Snack'">
                              <path d="M6 3h12l-1.5 3L18 9v10l1 2H5l1-2V9l1.5-3z" />
                              <path d="M7.5 6h9M9 13.5c1.5-1.5 4.5-1.5 6 0" />
                            </template>
                          </svg>
                        </span>

                        <span class="min-w-0 flex-1">
                          <span class="line-clamp-2 font-medium">{{ item.name }}</span>
                          <span class="mt-0.5 flex flex-wrap items-center gap-x-2 text-sm">
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
                        <span class="shrink-0 font-semibold tabular-nums">{{
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
               so the panel heading stays quiet; in the detail view the location name is the h3. -->
          <h3 class="text-sm font-semibold uppercase tracking-[0.2em] text-secondary">
            Alle Automaten
          </h3>
          <template v-if="status !== 'error'">
            <p class="mt-2 text-muted-foreground">
              Such dir einen Automaten aus. Hier steht dann, was drin ist.
            </p>
            <label for="machine-search" class="mt-3 block text-sm font-semibold">PLZ oder Ort</label>
            <!-- text-base: 16 px keeps iOS from zooming into the field. -->
            <input
              id="machine-search"
              :ref="searchRef"
              v-model="query"
              type="search"
              placeholder="z. B. 63607 oder Wächtersbach"
              autocomplete="off"
              enterkeyhint="search"
              class="mt-1 min-h-11 w-full rounded-lg border border-input bg-background px-3 text-base text-foreground placeholder:text-muted-foreground"
            />
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
