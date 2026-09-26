<script setup lang="ts">
// Smoke intro (docs/PLAN.md §4.3 "Smoke intro"): on a full page load the site sits behind a cloud
// of smoke that holds while `hold` is true (the 18+ dialog is open) and then blows apart.
// One canvas, puff sprites from the shared texture (src/smoke.ts), one rAF loop that stops
// and emits `done` at the end (App.vue unmounts the layer). App.vue does not mount it at all under
// prefers-reduced-motion. Colours come from the main.css tokens.
import { onMounted, onUnmounted, ref } from 'vue'
import { clamp01, rand, smokeSprites, SPRITE_PX, SPRITES } from '../smoke'

const props = defineProps<{ hold: boolean }>()
const emit = defineEmits<{ done: [] }>()

const base = ref<HTMLDivElement | null>(null)
const canvas = ref<HTMLCanvasElement | null>(null)

const DISSOLVE_MS = 1400 // §4.3: 1.2–1.6 s; page legible by ~1 s (see alpha curves in frame())
// Each puff's own dissolve runs DISSOLVE_MS × 0.8–1.12 (§4.3 "Randomness"), so the last one ends
// by ~1.57 s.
const PUFFS = 36

// --- puffs ---------------------------------------------------------------------------------------
interface Puff {
  x: number // centre, fraction of viewport width
  y: number // centre, fraction of viewport height
  size: number // diameter, fraction of max(viewport w, h)
  rot: number
  spin: number // rad/ms while holding
  phase: number
  swayX: number // hold sway frequencies, rad/ms
  swayY: number
  alpha: number
  dx: number // unit direction away from the viewport centre
  dy: number
  dist: number // how far it is blown, fraction of the viewport diagonal
  lag: number // fade delay (fraction of the dissolve), grows towards the edges: the centre clears first
  dur: number // this puff's dissolve, ms
  wobble: number // sideways wobble along the path, fraction of the viewport diagonal
  wobbleF: number // wobble cycles over the path
  sprite: number
}

function makePuffs(): Puff[] {
  const puffs: Puff[] = []
  const n = Math.round(Math.sqrt(PUFFS))
  for (let i = 0; i < PUFFS; i++) {
    const gx = (i % n) + Math.random()
    const gy = Math.floor(i / n) + Math.random()
    const x = -0.1 + (gx / n) * 1.2
    const y = -0.1 + (gy / n) * 1.2
    const ang = Math.atan2(y - 0.5, x - 0.5) + rand(-0.5, 0.5)
    puffs.push({
      x,
      y,
      size: rand(0.4, 0.75),
      rot: Math.random() * Math.PI * 2,
      spin: rand(-0.00008, 0.00008),
      phase: Math.random() * Math.PI * 2,
      swayX: rand(0.0003, 0.0007),
      swayY: rand(0.0003, 0.0007),
      alpha: rand(0.4, 0.75),
      dx: Math.cos(ang),
      dy: Math.sin(ang),
      dist: rand(0.3, 0.65),
      lag: 0.2 * Math.min(1, Math.hypot(x - 0.5, y - 0.5) / 0.75),
      dur: DISSOLVE_MS * rand(0.8, 1.12),
      wobble: rand(0.01, 0.035),
      wobbleF: rand(0.6, 1.6),
      sprite: i % SPRITES,
    })
  }
  return puffs
}

const easeOutCubic = (p: number) => 1 - (1 - p) ** 3
const smoothstep = (a: number, b: number, x: number) => {
  const t = clamp01((x - a) / (b - a))
  return t * t * (3 - 2 * t)
}

let raf = 0
let ctx: CanvasRenderingContext2D | null = null
let sprites: HTMLCanvasElement[] = []
let puffs: Puff[] = []
let w = 0
let h = 0
let start = -1 // dissolve start (rAF time), -1 while holding
let end = 0 // longest puff dissolve, ms

// ponytail: canvas at 1 device pixel regardless of DPR (§4.3 allows ≤ 2). The sprites are 256 px
// drawn at 300–1000 px, so a denser canvas adds fill cost on phones without adding detail. Raise
// SPRITE_PX and the DPR together if the smoke ever needs to look sharper.
function resize() {
  const c = canvas.value
  if (!c) return
  // the canvas's own CSS box, so one canvas pixel is one CSS pixel on both axes (1:1, §4.3)
  w = c.width = c.clientWidth
  h = c.height = c.clientHeight
}

function frame(now: number) {
  if (!ctx) return
  if (start < 0 && !props.hold) start = now
  const p = start < 0 ? 0 : Math.min(1, (now - start) / DISSOLVE_MS)

  // Motion eases out (a blown-apart puff decelerates). The opaque base holds a beat, then clears
  // by 0.6 × 1.4 s ≈ 0.84 s; puffs fade centre-first, all under ~10 % by 1 s and at 0 by the end.
  if (base.value) base.value.style.opacity = String(1 - smoothstep(0, 0.6, p))

  const big = Math.max(w, h)
  const diag = Math.hypot(w, h)
  ctx.setTransform(1, 0, 0, 1, 0, 0)
  ctx.clearRect(0, 0, w, h)
  for (const q of puffs) {
    const pq = start < 0 ? 0 : Math.min(1, (now - start) / q.dur)
    const e = easeOutCubic(pq)
    const sway = Math.sin(now * q.swayX + q.phase)
    // blown outward along (dx, dy), wobbling sideways (perpendicular) on the way
    const wob = Math.sin(e * Math.PI * 2 * q.wobbleF + q.phase) * q.wobble * diag * e
    const x = q.x * w + sway * 0.02 * w + q.dx * q.dist * diag * e - q.dy * wob
    const y = q.y * h + Math.cos(now * q.swayY + q.phase) * 0.02 * h + q.dy * q.dist * diag * e + q.dx * wob
    const s = ((q.size * big) / SPRITE_PX) * (1 + 0.03 * sway + 0.7 * e)
    const r = q.rot + q.spin * now + q.spin * 900 * e
    const cos = Math.cos(r) * s
    const sin = Math.sin(r) * s
    ctx.globalAlpha = q.alpha * (1 - clamp01((pq - q.lag) / (1 - q.lag))) ** 2.2
    ctx.setTransform(cos, sin, -sin, cos, x, y)
    ctx.drawImage(sprites[q.sprite]!, -SPRITE_PX / 2, -SPRITE_PX / 2)
  }

  if (start >= 0 && now - start >= end) {
    raf = 0
    emit('done')
    return
  }
  raf = requestAnimationFrame(frame)
}

onMounted(() => {
  ctx = canvas.value?.getContext('2d') ?? null
  if (!ctx) {
    emit('done')
    return
  }
  resize()
  sprites = smokeSprites()
  puffs = makePuffs()
  end = Math.max(...puffs.map((q) => q.dur))
  window.addEventListener('resize', resize)
  raf = requestAnimationFrame(frame)
})

onUnmounted(() => {
  cancelAnimationFrame(raf)
  window.removeEventListener('resize', resize)
  ctx = null
  sprites = []
})
</script>

<template>
  <!-- Below the header's skip link (z 1300) and the dialog's top layer, above everything else.
       Never blocks: no pointer events, hidden from assistive tech, nothing focusable. -->
  <div class="smoke-intro pointer-events-none fixed inset-0 z-[1250]" aria-hidden="true">
    <div ref="base" class="absolute inset-0 bg-background" />
    <canvas ref="canvas" class="absolute inset-0 size-full" />
  </div>
</template>
