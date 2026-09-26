<script setup lang="ts">
// Organic divider (docs/PLAN.md §4.3 "Organic divider line", "Glowing dividers", "Smoking
// dividers"): a softly wavering violet → rose → violet thread with its lavender glow, plus the
// smoke rim (.divider-smoke). Placement and z-order come from the caller's classes on the root.
//
// Shape: the centre line is 2–3 sine harmonics, the thickness 2–3 others, all a whole number of
// cycles per period P and with random phases per divider and load, so the path is exactly periodic.
// Drawn as a filled shape between two offset curves (≈ 0.25–3 px), not a stroke.
// Motion: the SVG is at least one period wider than the divider and only translateX animates, by
// exactly P (main.css .thread-drift), so the loop is seamless and nothing repaints per frame.
// Colour: the gradient must stay put while the wave drifts, so the thread is drawn twice: a violet
// copy in a window masked with the end fades, and a rose copy in a window masked 20 % → 50 % → 80 %
// on top (rose over violet at alpha a = the old gradient's linear mix). Both drift with the same
// custom properties, so they stay in sync. The glow is a copy blurred once in SVG (static) whose
// wrapper pulses its opacity.
import { useId } from 'vue'
import { rand, smokeRimStyle } from '../smoke'

const rim = smokeRimStyle()
const filterId = `thread-glow-${useId()}`

const H = 48 // thread box height: amplitude 5 + glow band 6.5 + blur reach, both sides
const CY = H / 2
const STEP = 8 // px between samples: the chord error on these curves is < 0.05 px
const P = STEP * Math.round(rand(800, 1200) / STEP) // period, a whole number of steps
// ponytail: the path covers 1.5 × the largest screen side plus one period, so window resizes and
// moderate zoom-out stay covered without regenerating; beyond that the far right end, already
// inside the faded 20 %, goes blank. Regenerate on resize if that ever shows.
const W = Math.ceil((1.5 * Math.max(window.innerWidth, screen.width, screen.height) + P) / STEP) * STEP

const pick = <T,>(xs: T[]) => xs[Math.floor(Math.random() * xs.length)]!

// A sum of sines with `ks` whole cycles per period, random phases, normalised over one period
// into [lo, hi].
function harmonics(ks: number[], weights: number[], lo: number, hi: number) {
  const ph = ks.map(() => rand(0, 2 * Math.PI))
  const raw = (x: number) =>
    ks.reduce((s, k, i) => s + weights[i]! * Math.sin((2 * Math.PI * k * x) / P + ph[i]!), 0)
  let min = Infinity
  let max = -Infinity
  for (let x = 0; x < P; x += STEP / 2) {
    const v = raw(x)
    if (v < min) min = v
    if (v > max) max = v
  }
  return (x: number) => lo + ((raw(x) - min) / (max - min)) * (hi - lo)
}

// Centre: a long dominant undulation (P/2 or P/3, i.e. ~270–600 px), one shorter overtone,
// sometimes a third; ±3–5 px.
const A = rand(3, 5)
const k1 = pick([2, 3])
const k2 = k1 + pick([1, 2, 3])
const centreKs = [k1, k2]
const centreWs = [1, rand(0.35, 0.6)]
if (Math.random() < 0.6) {
  centreKs.push(k2 + pick([2, 3, 4]))
  centreWs.push(rand(0.15, 0.3))
}
const centre = harmonics(centreKs, centreWs, -A, A)
// Thickness: its own harmonics (uncorrelated with the centre), 0.25–3 px: thins almost to nothing
// once or twice per period, never a gap.
const thick = harmonics([pick([1, 2]), pick([3, 4, 5]), pick([6, 7, 9])], [1, rand(0.5, 0.8), rand(0.2, 0.4)], 0.25, 3)

function band(extra: number): string {
  const top: string[] = []
  const bottom: string[] = []
  for (let x = 0; x <= W; x += STEP) {
    const c = CY + centre(x)
    const h = thick(x) / 2 + extra
    top.push(`${x},${(c - h).toFixed(2)}`)
    bottom.push(`${x},${(c + h).toFixed(2)}`)
  }
  return `M${top.join('L')}L${bottom.reverse().join('L')}Z`
}
const line = band(0)
const glow = band(5) // the old glow band was ±6 px around a 2 px line

// Drift: 12–20 px/s (≈ 40–100 s per period), left or right, random phase.
const dur = P / rand(12, 20)
const style = {
  '--thread-w': `${W}px`,
  '--thread-p': `${P}px`,
  '--thread-dur': `${dur.toFixed(1)}s`,
  '--thread-delay': `${(-rand(0, dur)).toFixed(1)}s`,
  '--thread-dir': Math.random() < 0.5 ? 'normal' : 'reverse',
}
</script>

<template>
  <div aria-hidden="true" :style="style">
    <span class="divider-smoke" :style="rim"><span><span /></span><span><span /></span></span>
    <span class="thread">
      <span class="thread-drift">
        <span class="thread-glow">
          <svg :width="W" :height="H">
            <filter :id="filterId" filterUnits="userSpaceOnUse" x="0" y="0" :width="W" :height="H">
              <feGaussianBlur stdDeviation="5" />
            </filter>
            <path :d="glow" :filter="`url(#${filterId})`" />
          </svg>
        </span>
        <svg :width="W" :height="H"><path :d="line" /></svg>
      </span>
    </span>
    <span class="thread thread-rose">
      <span class="thread-drift">
        <svg :width="W" :height="H"><path :d="line" /></svg>
      </span>
    </span>
  </div>
</template>
