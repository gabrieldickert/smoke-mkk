<script setup lang="ts">
import { computed } from 'vue'
import type { Machine } from '../api'
import CardSpotlight from './ui/CardSpotlight.vue'

const props = defineProps<{ machine: Machine; selected: boolean }>()
const emit = defineEmits<{ select: [] }>()

const nameId = computed(() => `machine-${props.machine.id}`)
const address = computed(() =>
  [props.machine.street, `${props.machine.postalCode} ${props.machine.city}`]
    .filter(Boolean)
    .join(', '),
)
</script>

<template>
  <CardSpotlight
    :class="[
      'border-border bg-card text-card-foreground transition-[border-color,box-shadow] duration-200 hover:border-secondary/60 dark:bg-card dark:text-card-foreground',
      selected && 'border-secondary shadow-[0_0_24px_var(--glow)]',
    ]"
    slot-class="flex w-full flex-col gap-3 p-5"
    gradient-color="var(--spotlight)"
    :gradient-opacity="1"
  >
    <h3 class="text-lg font-bold">
      <!-- The ::after stretches the button over the whole card, so a click anywhere selects. -->
      <button
        :id="nameId"
        type="button"
        :aria-pressed="selected"
        class="cursor-pointer text-left after:absolute after:inset-0 after:rounded-xl after:content-[''] focus-visible:outline-none focus-visible:after:outline-2 focus-visible:after:-outline-offset-2 focus-visible:after:outline-ring"
        @click="emit('select')"
      >
        {{ machine.name }}
      </button>
    </h3>
    <address class="text-sm not-italic text-muted-foreground">{{ address }}</address>
    <a
      v-if="machine.googleMapsUrl"
      :href="machine.googleMapsUrl"
      target="_blank"
      rel="noopener"
      :aria-describedby="nameId"
      class="relative z-10 inline-flex min-h-11 w-fit items-center gap-2 rounded-lg px-1 font-semibold text-secondary transition-colors duration-200 hover:text-foreground"
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
  </CardSpotlight>
</template>
