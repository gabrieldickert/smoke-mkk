<script setup lang="ts">
// Section fade-in on scroll, once (docs/PLAN.md §4.3 "Section transitions"; no fade-out since F10:
// re-animating on every scroll pass slowed scanning).
// Timing per ui-ux-pro-max motion.csv "Scroll Reveal / Standard": 400–600 ms, y 24, ease-out.
// `amount`: how much must be visible before it plays (motion-v: 'some' | 'all' | fraction). Blocks
// taller than the viewport pass 'some' (F24: the phone map + panel block is ~2.3 screens tall, so
// 0.2 kept the map hidden after the section was reached).
import { motion, useReducedMotion } from 'motion-v'

const { amount = 0.2 } = defineProps<{ amount?: 'some' | 'all' | number }>()
const reduceMotion = useReducedMotion()
</script>

<template>
  <div v-if="reduceMotion"><slot /></div>
  <motion.div
    v-else
    :initial="{ opacity: 0, y: 24 }"
    :while-in-view="{ opacity: 1, y: 0 }"
    :in-view-options="{ once: true, amount }"
    :transition="{ duration: 0.5, ease: 'easeOut' }"
  >
    <slot />
  </motion.div>
</template>
