<script setup lang="ts">
import { motion, useReducedMotion } from 'motion-v'
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { fetchMachines, type Machine } from '../api'
import InventoryPanel from '../components/InventoryPanel.vue'
import MachineMap from '../components/MachineMap.vue'
import Reveal from '../components/Reveal.vue'
import SocialLinks from '../components/SocialLinks.vue'
import AuroraBackground from '../components/ui/AuroraBackground.vue'
import FlipWords from '../components/ui/FlipWords.vue'

const WORDS = ['Vapes', 'Tabak', 'Rauchzubehör', 'Drinks', 'Snacks']

const reduceMotion = useReducedMotion()
const machines = ref<Machine[]>([])
const status = ref<'loading' | 'ready' | 'error'>('loading')
const selectedId = ref<number | null>(null)
const selected = computed(() => machines.value.find((m) => m.id === selectedId.value) ?? null)

// Visitor position (docs/PLAN.md §4.3 "Nearest machine marking"): memory only, never stored or sent.
type Point = Pick<Machine, 'lat' | 'lng'>
const position = ref<Point | null>(null)
// Straight-line km, flat-earth (longitude scaled by cos(lat)); plenty within one district.
const KM_PER_DEGREE = 111.195
const km = (a: Point, b: Point) =>
  Math.hypot(a.lat - b.lat, (a.lng - b.lng) * Math.cos((a.lat * Math.PI) / 180)) * KM_PER_DEGREE
const distances = computed(() => {
  const here = position.value
  return here ? new Map(machines.value.map((m) => [m.id, km(here, m)])) : null
})
const nearestId = computed(() => {
  let best: number | null = null
  let bestKm = Infinity
  for (const [id, d] of distances.value ?? []) if (d < bestKm) [best, bestKm] = [id, d]
  return best
})
const mapSection = ref<HTMLElement | null>(null)
const panelWrap = ref<HTMLElement | null>(null)
const wide = window.matchMedia('(min-width: 768px)') // Tailwind md: map and panel side by side

// The first-screen caret is hidden once the map section is reached: while #standorte has entered
// the viewport or lies above it. One observer; it fires on entering and leaving, which is all
// this needs.
const caretHidden = ref(false)
let caretObserver: IntersectionObserver | null = null

onMounted(async () => {
  caretObserver = new IntersectionObserver((entries) => {
    const e = entries[entries.length - 1]
    if (e) caretHidden.value = e.isIntersecting || e.boundingClientRect.top < 0
  })
  if (mapSection.value) caretObserver.observe(mapSection.value)
  // Never prompt on load: only a permission the visitor granted earlier is used, and then only to
  // mark and sort (no selection, no scrolling).
  navigator.permissions
    ?.query({ name: 'geolocation' })
    .then((p) => p.state === 'granted' && locate(false))
    .catch(() => {})
  try {
    machines.value = await fetchMachines()
    status.value = 'ready'
  } catch {
    status.value = 'error'
  }
})

onUnmounted(() => caretObserver?.disconnect())

function scrollToMap() {
  mapSection.value?.scrollIntoView({
    behavior: reduceMotion.value ? 'auto' : 'smooth',
    block: 'start',
  })
}

// One selection path for marker, panel list and CTA. Below md the panel sits under the map, so a
// selection brings it into view (scroll-mt-20 keeps it clear of the sticky header).
function select(id: number | null) {
  selectedId.value = id
  if (id == null || wide.matches) return
  panelWrap.value?.scrollIntoView({
    behavior: reduceMotion.value ? 'auto' : 'smooth',
    block: 'start',
  })
}

// Denied, unavailable or timed out: nothing happens, the map is already on screen.
function locate(selectNearest: boolean) {
  navigator.geolocation?.getCurrentPosition(
    ({ coords }) => {
      position.value = { lat: coords.latitude, lng: coords.longitude }
      if (selectNearest && nearestId.value != null) select(nearestId.value)
    },
    () => {},
    { timeout: 10000, maximumAge: 600000 },
  )
}

// Hero CTA and pitch-band caret. A plain hash jump would scroll to the right place (router.ts /
// scroll-mt-20), but it does not focus the section; this does, so the next Tab continues in the
// map, not in the hero.
function focusMap() {
  scrollToMap()
  mapSection.value?.focus({ preventScroll: true })
}

// The caret fades out at once on click; the observer keeps it hidden while the map is in view.
function caretToMap() {
  caretHidden.value = true
  focusMap()
}

// Only the hero CTA asks for the location (the prompt comes from this click), then selects the
// nearest machine. The caret never prompts.
function jumpToMap() {
  focusMap()
  if (status.value !== 'error') locate(true)
}
</script>

<template>
  <div class="aurora-everywhere">
    <!-- Single element root (no comment above it): App.vue wraps RouterView in <Transition mode="out-in">. -->
    <!-- TRIAL "aurora-everywhere" (docs/PLAN.md §4.3 "Aurora on all sections"): the hero's aurora
         layer becomes one fixed backdrop behind the whole page (main.css). Revert = delete the
         class on this root div and the matching block in main.css. -->
    <!-- First screen = hero + pitch band (owner, docs/PLAN.md §4.3): together they fill the screen
         below the header, split 60 % hero / 40 % pitch band (the upper divider belongs to the pitch
         share), content centred in each, the caret at the bottom; the map section starts right below
         the fold. Basis 0 (unitless: 0% would resolve to content height, the wrapper has only a
         min-height) + min-height auto: a share never shrinks below its content, so the ratio
         drifts instead of clipping, and shorter phones grow the wrapper (min-h), never clip. -->
    <div class="relative flex min-h-[calc(100svh-4rem)] flex-col">
      <!-- overflow-x-clip: FlipWords' leave animation scales the word 2x; without the clip a long
           word would briefly widen the page on phones. clip (not hidden) keeps y visible. -->
      <AuroraBackground
        class="aurora-brand h-auto flex-[6_1_0] overflow-x-clip bg-background px-4 text-foreground dark:bg-background"
      >
        <!-- Vertical padding lives here, not on the flex item: padding on the item would count
             towards its zero flex basis and skew the 60/40 split. flex-1: fills the hero share, so
             the action group can sit at its bottom (owner, docs/PLAN.md §4.3 "Hero actions at the
             bottom"). -->
        <Reveal class="relative z-10 flex w-full flex-1 flex-col items-center pt-2 pb-6 text-center">
          <!-- Three rows (owner, docs/PLAN.md §4.3 "Hero actions at the bottom"): brand at the top,
               the CTA centred in the flex-1 middle row (equal gaps above and below, py-6 = the
               1.5rem minimum on short screens), the social block at the bottom, 1.5rem (pb-6)
               above the glowing divider. DOM and focus order unchanged. -->
          <div class="flex flex-col items-center">
            <img
              src="/logo.webp"
              alt="SMOKE"
              width="96"
              height="96"
              class="size-24 rounded-3xl shadow-[0_0_64px_var(--glow)]"
            />
            <!-- Fluid size: RAUCHZUBEHÖR is ~7.6em wide incl. FlipWords' trailing nbsp, plus its px-2
                 (measured: 292 px of a 343 px content box at 375 px). It must fit one line from 320 px
                 up, so the size follows the viewport and caps at 3.5rem from ~550 px, so
                 the hero (with its 1.5rem gaps) fits its 60 % at 1280×800.
                 min-h + fixed leading reserve the line while FlipWords swaps words (v-show gap). -->
            <h1
              class="mt-4 min-h-[1.1em] text-[length:clamp(1.75rem,calc((100vw_-_3rem)/9),3.5rem)] leading-[1.1] font-bold uppercase tracking-tight"
            >
              <span class="sr-only">{{ WORDS.join(' · ') }}</span>
              <!-- Reduced motion: every word stays whole; a line may only break after a separator. -->
              <span
                v-if="reduceMotion"
                aria-hidden="true"
                class="block text-balance text-secondary [text-shadow:0_0_28px_var(--glow)]"
              >
                <template v-for="(word, i) in WORDS" :key="word">
                  <span class="whitespace-nowrap"
                    >{{ word
                    }}<span v-if="i < WORDS.length - 1" class="text-primary">&nbsp;·</span></span
                  >{{ ' ' }}
                </template>
              </span>
              <FlipWords
                v-else
                aria-hidden="true"
                :words="WORDS"
                :duration="2500"
                class="text-secondary [text-shadow:0_0_28px_var(--glow)] dark:text-secondary"
              />
            </h1>
            <!-- Always rendered, hidden (visually and from AT) until there is a count: the sentence
                 itself reserves its one- or two-line height, so nothing below moves when the API
                 answers — a direct load of /#standorte has already scrolled by then. -->
            <!-- Heroic tagline (owner, docs/PLAN.md §4.3): display type, second only to the headline
                 (ui-ux-pro-max type hierarchy: h1 36/60 px > count 30/48 px > region 24/36 px). The
                 count keeps its lower-case "x" (owner's wording). -->
            <p
              :class="{ invisible: !machines.length }"
              class="mt-3 max-w-4xl text-2xl leading-tight font-bold uppercase tracking-tight text-balance text-foreground sm:text-4xl"
            >
              <span class="text-3xl normal-case text-secondary [text-shadow:0_0_28px_var(--glow)] sm:text-5xl"
                >{{ machines.length }}x</span
              >
              <!-- One line on purpose: Vue drops whitespace-only text containing a newline, which
                   would remove the only break between the two units. -->
              im <span class="whitespace-nowrap">Main-Kinzig-Kreis</span> <span class="whitespace-nowrap">und Umgebung</span>
            </p>
          </div>
          <div class="flex flex-1 items-center justify-center py-6">
            <a
              href="#standorte"
              class="inline-flex min-h-12 items-center gap-2 rounded-xl bg-primary px-5 py-3 text-base font-semibold uppercase tracking-wide text-primary-foreground shadow-[0_0_24px_var(--glow)] transition-colors duration-200 hover:bg-primary/85 sm:px-7 sm:text-lg"
              @click.exact.prevent="jumpToMap"
            >
              <svg
                class="size-5 shrink-0"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
                aria-hidden="true"
              >
                <path d="M20 10c0 6-8 12-8 12s-8-6-8-12a8 8 0 0 1 16 0z" />
                <circle cx="12" cy="10" r="3" />
              </svg>
              <span class="text-balance">Automat in deiner Nähe finden</span>
            </a>
          </div>
          <div class="flex flex-col items-center">
            <p class="text-sm text-muted-foreground text-balance">
              Folg uns, um nichts zu verpassen!
            </p>
            <div class="mt-2">
              <SocialLinks variant="cta" />
            </div>
          </div>
        </Reveal>
      </AuroraBackground>

      <!-- Glowing dividers on both boundaries, hero | pitch band and pitch band | map (owner,
           docs/PLAN.md §4.3 "Glowing dividers"); one class, styles in main.css. They sit outside the
           band's overflow-hidden so the glow is not clipped. -->
      <div class="flex flex-[4_1_0] flex-col">
        <div aria-hidden="true" class="glow-divider relative h-0.5 shrink-0" />

        <!-- Pitch band (F9): the middle section between hero and map. A soft radial glow between the
             two glowing dividers; all from tokens, no image, no new component. -->
        <!-- flex-1 of the pitch share, content centred; pb-12 keeps the text clear of the caret,
             which sits in that padding. The share is ~295 px at 1280×800 and 375×812. -->
        <section
          aria-labelledby="pitch-heading"
          class="relative isolate flex flex-1 flex-col justify-center overflow-hidden px-4 pt-2 pb-12"
        >
          <div
            aria-hidden="true"
            class="absolute inset-0 -z-10 bg-[radial-gradient(ellipse_at_center,var(--spotlight),transparent_65%)] opacity-60"
          />
          <Reveal class="mx-auto max-w-[40rem] text-center">
            <h2
              id="pitch-heading"
              class="text-2xl font-bold uppercase tracking-tight text-balance sm:text-4xl"
            >
              Spät dran? <span class="text-secondary [text-shadow:0_0_28px_var(--glow)]">Wir nicht.</span>
            </h2>
            <p class="mt-3 text-base leading-relaxed text-foreground/85 text-pretty sm:text-lg">
              Tanke zu, Kiosk zu, Kühlschrank leer? Unsere Automaten haben trotzdem auf. Kalte Drinks, Snacks und der Rest vom Sortiment, auch sonntags und nachts um drei.
            </p>
          </Reveal>
          <!-- Scroll cue at the bottom centre of the first screen: bounces three times once fully in
               view (i.e. after load), then rests; no motion when reduced. The bounce is on the inner
               span: the link's own transform centres it. Scroll + focus like the CTA, no location prompt.
               Hidden once the map section is reached: the opacity fades (300 ms), then visibility
               follows, so the invisible caret is neither clickable nor in the tab order; showing
               flips visibility first. Instant with reduced motion (global rule in main.css). -->
          <a
            href="#standorte"
            aria-label="Unsere Automaten"
            :class="[
              'absolute bottom-1 left-1/2 grid size-11 -translate-x-1/2 place-items-center rounded-full text-muted-foreground hover:text-foreground',
              caretHidden
                ? 'invisible opacity-0 [transition:color_200ms,opacity_300ms,visibility_0s_linear_300ms]'
                : '[transition:color_200ms,opacity_300ms,visibility_0s]',
            ]"
            @click.exact.prevent="caretToMap"
          >
            <motion.span
              class="block"
              :while-in-view="reduceMotion ? undefined : { y: [0, 8, 0] }"
              :in-view-options="{ once: true, amount: 1 }"
              :transition="{ duration: 0.6, ease: 'easeInOut', repeat: 2, delay: 0.3 }"
            >
              <svg
                class="size-7"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
                aria-hidden="true"
              >
                <path d="m6 9 6 6 6-6" />
              </svg>
            </motion.span>
          </a>
        </section>
      </div>
      <div aria-hidden="true" class="glow-divider absolute inset-x-0 bottom-0 h-0.5" />
    </div>

    <section
      id="standorte"
      ref="mapSection"
      aria-labelledby="standorte-heading"
      tabindex="-1"
      class="mx-auto max-w-6xl scroll-mt-20 px-4 py-16 focus:outline-none"
    >
      <Reveal>
        <h2 id="standorte-heading" class="text-3xl font-bold uppercase tracking-tight sm:text-4xl">
          Unsere Automaten
        </h2>
        <p class="mt-3 max-w-2xl text-muted-foreground text-pretty sm:text-lg">
          <strong class="font-semibold text-foreground">Hast du Lust auf Snacks, Drinks and more?</strong>
          Such dir einen Automaten in deiner Nähe aus und schau, ob deine Lieblingssachen verfügbar sind!
        </p>
      </Reveal>

      <Reveal class="mt-8 grid gap-6 md:grid-cols-5">
        <div
          class="relative isolate h-80 overflow-hidden rounded-xl border border-border sm:h-96 md:col-span-3 md:h-[30rem]"
        >
          <MachineMap
            :machines="machines"
            :selected-id="selectedId"
            :position="position"
            :nearest-id="nearestId"
            @select="select"
          />
        </div>
        <div ref="panelWrap" class="scroll-mt-20 md:col-span-2 md:h-[30rem]">
          <InventoryPanel
            :machines="machines"
            :status="status"
            :machine="selected"
            :distances="distances"
            :nearest-id="nearestId"
            @select="select"
          />
        </div>
      </Reveal>
    </section>
  </div>
</template>
