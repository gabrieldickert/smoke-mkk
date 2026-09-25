<script setup lang="ts">
import { useReducedMotion } from 'motion-v'
import { computed, onMounted, ref } from 'vue'
import { fetchMachines, type Machine } from '../api'
import InventoryPanel from '../components/InventoryPanel.vue'
import MachineCard from '../components/MachineCard.vue'
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
const mapSection = ref<HTMLElement | null>(null)

onMounted(async () => {
  try {
    machines.value = await fetchMachines()
    status.value = 'ready'
  } catch {
    status.value = 'error'
  }
})

function scrollToMap() {
  mapSection.value?.scrollIntoView({
    behavior: reduceMotion.value ? 'auto' : 'smooth',
    block: 'start',
  })
}

function selectFromCard(id: number) {
  selectedId.value = id
  scrollToMap()
}

// Hero CTA. A plain hash jump would scroll to the right place (router.ts / scroll-mt-20), but it
// does not focus the section; this does, so the next Tab continues in the map, not in the hero.
function jumpToMap() {
  scrollToMap()
  mapSection.value?.focus({ preventScroll: true })
}
</script>

<template>
  <div>
    <!-- Single element root (no comment above it): App.vue wraps RouterView in <Transition mode="out-in">. -->
    <!-- overflow-x-clip: FlipWords' leave animation scales the word 2x; without the clip a long
         word would briefly widen the page on phones. clip (not hidden) keeps y visible. -->
    <AuroraBackground
      class="aurora-brand h-auto min-h-[calc(100svh-4rem)] overflow-x-clip bg-background px-4 py-16 text-foreground dark:bg-background"
    >
      <Reveal class="relative z-10 flex flex-col items-center text-center">
        <img
          src="/logo.png"
          alt="SMOKE"
          width="600"
          height="600"
          class="size-36 rounded-3xl shadow-[0_0_64px_var(--glow)] sm:size-48"
        />
        <!-- Fluid size: RAUCHZUBEHÖR is ~7.6em wide incl. FlipWords' trailing nbsp, plus its px-2
             (measured: 292 px of a 343 px content box at 375 px). It must fit one line from 320 px
             up, so the size follows the viewport and caps at text-7xl (4.5rem) from ~700 px.
             min-h + fixed leading reserve the line while FlipWords swaps words (v-show gap). -->
        <h1
          class="mt-8 min-h-[1.1em] text-[length:clamp(1.75rem,calc((100vw_-_3rem)/9),4.5rem)] leading-[1.1] font-bold uppercase tracking-tight"
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
        <p
          :class="{ invisible: !machines.length }"
          class="mt-5 max-w-xl text-lg text-foreground/85 text-balance sm:text-xl"
        >
          Hat immer auf. {{ machines.length }}
          {{ machines.length === 1 ? 'Automat' : 'Automaten' }} zwischen Rodgau und Fulda.
        </p>
        <a
          href="#standorte"
          class="mt-8 inline-flex min-h-12 items-center gap-2 rounded-xl bg-primary px-7 py-3 text-lg font-semibold uppercase tracking-wide text-primary-foreground shadow-[0_0_24px_var(--glow)] transition-colors duration-200 hover:bg-primary/85"
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
          Automat finden
        </a>
        <p class="mt-10 text-sm text-muted-foreground text-balance">
          Neue Standorte gibt's zuerst auf Instagram und TikTok.
        </p>
        <div class="mt-3">
          <SocialLinks variant="cta" />
        </div>
      </Reveal>
    </AuroraBackground>

    <section
      id="standorte"
      ref="mapSection"
      aria-labelledby="standorte-heading"
      tabindex="-1"
      class="mx-auto max-w-6xl scroll-mt-20 px-4 py-16 focus:outline-none"
    >
      <Reveal>
        <h2 id="standorte-heading" class="text-3xl font-bold uppercase tracking-tight sm:text-4xl">
          Standorte
        </h2>
        <p class="mt-3 max-w-2xl text-muted-foreground text-pretty sm:text-lg">
          <strong class="font-semibold text-foreground">Erst schauen, dann losgehen.</strong>
          Jeder Pin zeigt dir, was gerade im Automaten ist.
        </p>
      </Reveal>

      <!-- once: the map never fades out mid-use. -->
      <Reveal once class="mt-8 grid gap-6 md:grid-cols-5">
        <div
          class="relative isolate h-80 overflow-hidden rounded-xl border border-border sm:h-96 md:col-span-3 md:h-[30rem]"
        >
          <MachineMap
            :machines="machines"
            :selected-id="selectedId"
            @select="selectedId = $event"
          />
          <div
            v-if="status === 'error'"
            class="absolute inset-0 z-[1100] grid place-items-center bg-background/75 p-6"
          >
            <p role="alert" class="rounded-xl border border-border bg-card px-5 py-4 text-center">
              Die Standorte konnten nicht geladen werden.
            </p>
          </div>
        </div>
        <div class="md:col-span-2 md:h-[30rem]">
          <InventoryPanel :machine="selected" />
        </div>
      </Reveal>

      <ul
        v-if="status === 'loading'"
        class="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-3"
        aria-hidden="true"
      >
        <li v-for="n in 6" :key="n" class="h-36 animate-pulse rounded-xl bg-card" />
      </ul>
      <ul v-else-if="machines.length" class="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <li v-for="(machine, index) in machines" :key="machine.id">
          <Reveal :delay="index * 0.05" class="h-full">
            <MachineCard
              :machine="machine"
              :selected="machine.id === selectedId"
              @select="selectFromCard(machine.id)"
            />
          </Reveal>
        </li>
      </ul>
    </section>
  </div>
</template>
