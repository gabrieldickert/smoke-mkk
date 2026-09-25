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

const WORDS = ['Vapes', 'Drinks', 'Snacks']

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

function selectFromCard(id: number) {
  selectedId.value = id
  mapSection.value?.scrollIntoView({
    behavior: reduceMotion.value ? 'auto' : 'smooth',
    block: 'start',
  })
}
</script>

<template>
  <div>
    <!-- Single element root (no comment above it): App.vue wraps RouterView in <Transition mode="out-in">. -->
    <AuroraBackground
      class="aurora-brand h-auto min-h-[calc(100svh-4rem)] bg-background px-4 py-16 text-foreground dark:bg-background"
    >
      <Reveal class="relative z-10 flex flex-col items-center text-center">
        <img
          src="/logo.png"
          alt="SMOKE"
          width="600"
          height="600"
          class="size-36 rounded-3xl shadow-[0_0_64px_var(--glow)] sm:size-48"
        />
        <h1 class="mt-8 text-5xl font-bold uppercase tracking-tight sm:text-7xl">
          <span class="sr-only">{{ WORDS.join(' · ') }}</span>
          <span v-if="reduceMotion" aria-hidden="true" class="text-secondary">{{
            WORDS.join(' · ')
          }}</span>
          <FlipWords
            v-else
            aria-hidden="true"
            :words="WORDS"
            :duration="2500"
            class="text-secondary [text-shadow:0_0_28px_var(--glow)] dark:text-secondary"
          />
        </h1>
        <p
          v-if="machines.length"
          class="mt-5 max-w-xl text-lg text-foreground/85 text-balance sm:text-xl"
        >
          Rund um die Uhr. An {{ machines.length }} Standorten in Hessen.
        </p>
        <div class="mt-8">
          <SocialLinks variant="cta" />
        </div>
      </Reveal>
    </AuroraBackground>

    <section
      id="standorte"
      ref="mapSection"
      aria-labelledby="standorte-heading"
      class="mx-auto max-w-6xl scroll-mt-20 px-4 py-16"
    >
      <Reveal>
        <h2 id="standorte-heading" class="text-3xl font-bold uppercase tracking-tight sm:text-4xl">
          Standorte
        </h2>
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
