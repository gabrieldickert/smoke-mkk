<script setup lang="ts">
// Section fade-in on scroll, once (docs/PLAN.md §4.3 "Section transitions"; no fade-out since F10:
// re-animating on every scroll pass slowed scanning).
// Timing per ui-ux-pro-max motion.csv "Scroll Reveal / Standard": 400–600 ms, y 24, ease-out.
import { motion, useReducedMotion } from 'motion-v'

const reduceMotion = useReducedMotion()
</script>

<template>
  <div v-if="reduceMotion"><slot /></div>
  <motion.div
    v-else
    :initial="{ opacity: 0, y: 24 }"
    :while-in-view="{ opacity: 1, y: 0 }"
    :in-view-options="{ once: true, amount: 0.2 }"
    :transition="{ duration: 0.5, ease: 'easeOut' }"
  >
    <slot />
  </motion.div>
</template>
