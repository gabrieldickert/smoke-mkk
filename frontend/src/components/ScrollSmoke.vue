<script setup lang="ts">
// Scroll smoke (docs/PLAN.md §4.3 "Scroll smoke and smoking dividers"): scrolling puffs out small,
// faint wisps from the left and right viewport edges that drift against the scroll direction (and
// outward, away from the text) and fade in 0.8–1.2 s. Spawned by scroll distance, one per STEP px,
// at most MAX alive. Idle costs nothing: no rAF, no timer, a zero-size canvas, only the passive
// scroll listener. App.vue does not mount it under prefers-reduced-motion.
import { onMounted, onUnmounted, ref } from 'vue'
import { clamp01, rand, smokeSprites, SPRITE_PX, SPRITES } from '../smoke'

const STEP = 120 // px scrolled per wisp
const MAX = 12
const COLUMN = 672 // centred text column (40rem + gutters) the wisps stay out of

interface Wisp {
  born: number
  life: number // ms
  x: number
  y: number
  dx: number // total drift over the life, px
  dy: number
  size: number // px
  rot: number
  spin: number // rad over the life
  peak: number // alpha
  wobble: number // sideways wobble amplitude, px
  wobbleF: number // wobble cycles over the life
  phase: number
  sprite: number
}

const canvas = ref<HTMLCanvasElement | null>(null)
let ctx: CanvasRenderingContext2D | null = null
let wisps: Wisp[] = []
let raf = 0
let lastY = 0
let acc = 0
let side = 1

function spawn(dir: number, now: number) {
  const w = window.innerWidth
  const h = window.innerHeight
  // The outer band beside the text column; on phones the column is the whole width, so the wisp
  // barely enters (its core stays off-screen and only the thin, faint edge shows).
  const band = Math.min(200, Math.max(24, (w - COLUMN) / 2))
  const size = Math.min(340, Math.max(110, band * 1.6)) * rand(0.75, 1.25)
  side = -side
  const inset = band - size * rand(0.45, 0.6) // centre, so the dense core stays inside the band
  wisps.push({
    born: now,
    life: rand(800, 1200), // all ranges ±20–30 % (§4.3 "Randomness")
    x: side < 0 ? inset : w - inset,
    y: rand(0.15, 0.85) * h,
    dx: side * size * rand(0.06, 0.18), // outward, so the drift angle varies too
    dy: -dir * h * rand(0.1, 0.2), // scrolling down → wisps drift up, like a wake
    size,
    rot: Math.random() * Math.PI * 2,
    spin: rand(-0.6, 0.6),
    peak: rand(0.18, 0.28), // × the sprite's own alpha (≤ 1): peak ≲ 0.3 even where two overlap
    wobble: Math.min(size * rand(0.04, 0.1), band * 0.3), // never carries it into the column
    wobbleF: rand(0.5, 1.2),
    phase: Math.random() * Math.PI * 2,
    sprite: Math.floor(Math.random() * SPRITES),
  })
}

function onScroll() {
  const y = window.scrollY
  const dy = y - lastY
  lastY = y
  acc += Math.abs(dy)
  if (acc < STEP) return
  const now = performance.now()
  for (; acc >= STEP; acc -= STEP) if (wisps.length < MAX) spawn(Math.sign(dy) || 1, now)
  if (!raf && wisps.length) raf = requestAnimationFrame(frame)
}

const easeOut = (t: number) => 1 - (1 - t) ** 2

function frame(now: number) {
  const c = canvas.value
  if (!c || !ctx) return
  // the canvas's own CSS box, so one canvas pixel is one CSS pixel on both axes (1:1, §4.3)
  const w = c.clientWidth
  const h = c.clientHeight
  if (c.width !== w || c.height !== h) {
    c.width = w
    c.height = h
  } else {
    ctx.setTransform(1, 0, 0, 1, 0, 0)
    ctx.clearRect(0, 0, w, h)
  }
  wisps = wisps.filter((q) => now - q.born < q.life)
  const sprites = smokeSprites() // cached; the intro built them at load
  for (const q of wisps) {
    const t = clamp01((now - q.born) / q.life)
    const e = easeOut(t)
    // quick swell to the peak, then a longer fade out
    ctx.globalAlpha = q.peak * (t < 0.2 ? t / 0.2 : (1 - (t - 0.2) / 0.8) ** 1.5)
    const s = (q.size / SPRITE_PX) * (1 + 0.5 * e)
    const r = q.rot + q.spin * e
    const cos = Math.cos(r) * s
    const sin = Math.sin(r) * s
    const wob = Math.sin(t * Math.PI * 2 * q.wobbleF + q.phase) * q.wobble * t // grows from 0
    ctx.setTransform(cos, sin, -sin, cos, q.x + q.dx * e + wob, q.y + q.dy * e)
    ctx.drawImage(sprites[q.sprite]!, -SPRITE_PX / 2, -SPRITE_PX / 2)
  }
  if (wisps.length) {
    raf = requestAnimationFrame(frame)
  } else {
    raf = 0
    c.width = c.height = 0 // idle: nothing to composite, no backing store
  }
}

onMounted(() => {
  ctx = canvas.value?.getContext('2d') ?? null
  if (!ctx) return
  lastY = window.scrollY
  window.addEventListener('scroll', onScroll, { passive: true })
})

onUnmounted(() => {
  window.removeEventListener('scroll', onScroll)
  cancelAnimationFrame(raf)
  ctx = null
})
</script>

<template>
  <!-- Below the header (z 1200), the intro (z 1250) and the dialog's top layer; never blocks. -->
  <canvas
    ref="canvas"
    width="0"
    height="0"
    class="scroll-smoke pointer-events-none fixed inset-0 z-[1100] size-full"
    aria-hidden="true"
  />
</template>
