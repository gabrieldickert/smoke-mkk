<script setup lang="ts">
// Section fade in/out on scroll (docs/PLAN.md §4.3 "Section transitions").
// Timing per ui-ux-pro-max motion.csv "Scroll Reveal / Standard": 400–600 ms, y 24, ease-out.
import { motion, useReducedMotion } from 'motion-v'

const MAX_DELAY = 0.4 // s; stagger beyond ~8 items feels laggy

withDefaults(defineProps<{ once?: boolean; delay?: number }>(), { once: false, delay: 0 })

const reduceMotion = useReducedMotion()
</script>

<template>
  <div v-if="reduceMotion"><slot /></div>
  <motion.div
    v-else
    :initial="{ opacity: 0, y: 24 }"
    :while-in-view="{ opacity: 1, y: 0 }"
    :in-view-options="{ once, amount: 0.2 }"
    :transition="{ duration: 0.5, ease: 'easeOut', delay: Math.min(delay, MAX_DELAY) }"
  >
    <slot />
  </motion.div>
</template>
