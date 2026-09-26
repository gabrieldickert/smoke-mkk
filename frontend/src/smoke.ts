// Shared smoke texture (docs/PLAN.md §4.3 "Smoke intro", "Scroll smoke and smoking dividers"):
// the one texture generator in the tree. One tileable fBm noise field, freshly seeded on every page
// load, shapes (a) SPRITES round puff sprites (grey from --foreground with patches of --primary)
// for the intro and scroll-smoke canvases and (b) two seamless square tiles (violet / rose tinted)
// for the divider smoke rims. Everything is drawn at 1:1 aspect (uniform scale only, §4.3). Each is built on first use and cached for the page's lifetime.

export const SPRITE_PX = 256
export const SPRITES = 6
const FIELD = 256 // tileable fBm field, sampled by every sprite and tile
const SEED = (Math.random() * 0x7fffffff) | 0 // fresh noise per load (§4.3 "Randomness")
const OFFSET = Math.random() * FIELD // where in the field sprite 0 starts
export const rand = (a: number, b: number) => a + Math.random() * (b - a)

// --- noise ---------------------------------------------------------------------------------------
function hash(x: number, y: number, seed: number): number {
  let h = Math.imul(x, 374761393) + Math.imul(y, 668265263) + Math.imul(seed, 1442695041)
  h = Math.imul(h ^ (h >>> 13), 1274126177)
  return ((h ^ (h >>> 16)) >>> 0) / 4294967295
}

// Tileable fBm: 5 octaves of value noise, each on a periodic `cells` × `cells` lattice (4 … 64
// cells across the field), smoothstep-interpolated.
function buildField(): Float32Array {
  const f = new Float32Array(FIELD * FIELD)
  let amp = 0.5
  for (let o = 0, cells = 4; o < 5; o++, cells *= 2, amp *= 0.5) {
    const lat = new Float32Array(cells * cells)
    for (let j = 0; j < lat.length; j++) lat[j] = hash(j % cells, Math.floor(j / cells), SEED + o)
    const step = FIELD / cells
    for (let y = 0; y < FIELD; y++) {
      const y0 = Math.floor(y / step)
      const v = y / step - y0
      const sv = v * v * (3 - 2 * v)
      const r0 = y0 * cells
      const r1 = ((y0 + 1) % cells) * cells
      for (let x = 0; x < FIELD; x++) {
        const x0 = Math.floor(x / step)
        const u = x / step - x0
        const su = u * u * (3 - 2 * u)
        const x1 = (x0 + 1) % cells
        const a = lat[r0 + x0]!
        const b = lat[r0 + x1]!
        const c = lat[r1 + x0]!
        const d = lat[r1 + x1]!
        f[y * FIELD + x]! += amp * (a + (b - a) * su + (c - a) * sv + (a - b - c + d) * su * sv)
      }
    }
  }
  for (let j = 0; j < f.length; j++) f[j]! /= 0.97
  return f
}

// Bilinear sample with wrap-around; (u, v) in field pixels, >= -WRAP_PAD. FIELD is a power of two,
// so wrapping is a bit mask; the pad keeps `| 0` (truncation) equal to floor.
const WRAP_PAD = FIELD * 8
const MASK = FIELD - 1
function sample(f: Float32Array, u: number, v: number): number {
  u += WRAP_PAD
  v += WRAP_PAD
  const x = u | 0
  const y = v | 0
  const fx = u - x
  const fy = v - y
  const x0 = x & MASK
  const y0 = (y & MASK) * FIELD
  const x1 = (x0 + 1) & MASK
  const y1 = ((y + 1) & MASK) * FIELD
  const a = f[y0 + x0]!
  const b = f[y0 + x1]!
  const c = f[y1 + x0]!
  const d = f[y1 + x1]!
  return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy
}

export const clamp01 = (x: number) => (x < 0 ? 0 : x > 1 ? 1 : x)

type Rgb = [number, number, number]

// One puff: domain-warped noise inside a radial falloff whose edge is itself pushed around by the
// noise, so the silhouette is ragged and the inside streaky rather than a soft disc.
function renderSprite(f: Float32Array, i: number, smoke: Rgb, tint: Rgb): HTMLCanvasElement {
  const c = document.createElement('canvas')
  c.width = c.height = SPRITE_PX
  const ctx = c.getContext('2d')!
  const img = ctx.createImageData(SPRITE_PX, SPRITE_PX)
  const px = img.data
  const ox = OFFSET + i * 97.3
  const oy = OFFSET + i * 53.1
  const half = SPRITE_PX / 2
  for (let y = 0; y < SPRITE_PX; y++) {
    for (let x = 0; x < SPRITE_PX; x++) {
      const nx = (x - half) / half
      const ny = (y - half) / half
      const wx = sample(f, x * 0.55 + ox, y * 0.55 + oy) - 0.5
      const r = Math.sqrt(nx * nx + ny * ny) + wx * 0.9
      if (r >= 1) continue // outside the ragged edge: stays transparent (ImageData starts at 0)
      const fall = Math.pow((1 - r) / 0.75 > 1 ? 1 : (1 - r) / 0.75, 1.4)
      const wy = sample(f, x * 0.55 + oy + 131, y * 0.55 + ox + 71) - 0.5
      // billows: warped fBm; filaments: ridges (|n - 0.5| near 0) of a finer, harder-warped sample
      const dens = sample(f, x * 0.9 + wx * 140 + ox, y * 0.9 + wy * 140 + oy)
      const billow = clamp01((dens - 0.4) * 2.6)
      const ridge = 1 - Math.abs(sample(f, x * 1.6 + wy * 260 + oy, y * 1.6 + wx * 260 + ox) - 0.5) * 2
      const r2 = ridge * ridge
      const fil = r2 * r2 * r2
      const a = fall * clamp01(0.08 + 0.8 * billow * billow + 0.45 * fil * (0.4 + billow))
      const t = clamp01((sample(f, x * 0.3 + oy, y * 0.3 + ox) - 0.45) * 2.5) * 0.45
      const k = (y * SPRITE_PX + x) * 4
      px[k] = smoke[0] + (tint[0] - smoke[0]) * t
      px[k + 1] = smoke[1] + (tint[1] - smoke[1]) * t
      px[k + 2] = smoke[2] + (tint[2] - smoke[2]) * t
      px[k + 3] = a * 255
    }
  }
  ctx.putImageData(img, 0, 0)
  return c
}

// Token string (#hex, rgb(), oklch() …) → RGB, via the canvas colour parser on a 1 px scratch canvas.
function tokenRgb(name: string): Rgb {
  const c = document.createElement('canvas')
  c.width = c.height = 1
  const ctx = c.getContext('2d', { willReadFrequently: true })
  if (!ctx) return [200, 200, 210]
  ctx.fillStyle = getComputedStyle(document.documentElement).getPropertyValue(name).trim() || '#888'
  ctx.clearRect(0, 0, 1, 1)
  ctx.fillRect(0, 0, 1, 1)
  const d = ctx.getImageData(0, 0, 1, 1).data
  return [d[0]!, d[1]!, d[2]!]
}

// A seamless square tile (divider rims): the sprite's billows and filaments without the radial falloff. Every sample
// is taken at an integer multiple of (x, y) plus a periodic warp, so the tile wraps in both axes.
function renderTile(f: Float32Array, o: number, smoke: Rgb, tint: Rgb): HTMLCanvasElement {
  const c = document.createElement('canvas')
  c.width = c.height = FIELD
  const ctx = c.getContext('2d')!
  const img = ctx.createImageData(FIELD, FIELD)
  const px = img.data
  for (let y = 0; y < FIELD; y++) {
    for (let x = 0; x < FIELD; x++) {
      const wx = sample(f, x + o, y + o * 0.7) - 0.5
      const wy = sample(f, x + o * 1.3 + 131, y + o + 71) - 0.5
      const dens = sample(f, x + wx * 140 + o, y + wy * 140 + o)
      const billow = clamp01((dens - 0.35) * 2.4)
      const ridge = 1 - Math.abs(sample(f, 2 * x + wy * 260 + o, 2 * y + wx * 260 + o) - 0.5) * 2
      const r2 = ridge * ridge
      const fil = r2 * r2 * r2
      const a = clamp01(0.06 + 0.85 * billow * billow + 0.5 * fil * (0.3 + billow))
      const t = 0.35 + 0.5 * clamp01((sample(f, x + o * 2, y + o) - 0.4) * 2.5)
      const k = (y * FIELD + x) * 4
      px[k] = smoke[0] + (tint[0] - smoke[0]) * t
      px[k + 1] = smoke[1] + (tint[1] - smoke[1]) * t
      px[k + 2] = smoke[2] + (tint[2] - smoke[2]) * t
      px[k + 3] = a * 255
    }
  }
  ctx.putImageData(img, 0, 0)
  return c
}

// Box blur with wrap-around, in place, `passes` times per axis (3 passes ≈ a Gaussian), so a
// seamless tile stays seamless. One-off at tile generation, never per frame.
function blurWrap(a: Float32Array, r: number, passes: number) {
  const tmp = new Float32Array(a.length)
  const n = 2 * r + 1
  for (let p = 0; p < passes; p++) {
    for (const horizontal of [true, false]) {
      for (let line = 0; line < FIELD; line++) {
        const at = (i: number) => (horizontal ? line * FIELD + (i & MASK) : (i & MASK) * FIELD + line)
        let sum = 0
        for (let i = -r; i <= r; i++) sum += a[at(i)]!
        for (let i = 0; i < FIELD; i++) {
          tmp[at(i)] = sum / n
          sum += a[at(i + r + 1)]! - a[at(i - r + FIELD)]!
        }
      }
      a.set(tmp)
    }
  }
}

// The divider rims' reach mask: a seamless square alpha tile of broad, domain-warped blobs, so
// where it is dense the rim reaches its full height and where it is thin only the short base fade
// shows. Soft and low-contrast (owner: no hard cuts): a one-off blur and a gentle level curve, so
// the ragged edge is made of soft wisps, not cut-out shapes. Only alpha matters (CSS mask); same periodic
// sampling as renderTile, so it wraps in both axes.
const REACH_MEAN = 0.75
const REACH_SPREAD = 0.28
function renderReach(f: Float32Array, o: number): HTMLCanvasElement {
  const a = new Float32Array(FIELD * FIELD)
  for (let y = 0; y < FIELD; y++) {
    for (let x = 0; x < FIELD; x++) {
      const wx = sample(f, x + o + 57, y + o * 0.6 + 19) - 0.5
      const wy = sample(f, x + o * 0.8 + 101, y + o + 43) - 0.5
      a[y * FIELD + x] = sample(f, x + wx * 90 + o, y + wy * 90 + o)
    }
  }
  blurWrap(a, 6, 3)
  // Normalised per load (the field's mean and spread vary with the seed), so the average reach is
  // the same on every load: mean REACH_MEAN, soft spread REACH_SPREAD per standard deviation.
  let mean = 0
  for (const v of a) mean += v
  mean /= a.length
  let sd = 0
  for (const v of a) sd += (v - mean) ** 2
  sd = Math.sqrt(sd / a.length) || 1
  for (let j = 0; j < a.length; j++) {
    const n = clamp01(REACH_MEAN + (REACH_SPREAD * (a[j]! - mean)) / sd)
    a[j] = n * n * (3 - 2 * n)
  }
  const c = document.createElement('canvas')
  c.width = c.height = FIELD
  const ctx = c.getContext('2d')!
  const img = ctx.createImageData(FIELD, FIELD)
  const px = img.data
  for (let j = 0; j < a.length; j++) {
    px[j * 4] = px[j * 4 + 1] = px[j * 4 + 2] = 255
    px[j * 4 + 3] = a[j]! * 255
  }
  ctx.putImageData(img, 0, 0)
  return c
}

let field: Float32Array | null = null
let sprites: HTMLCanvasElement[] | null = null
let tiles: Promise<[string, string, string]> | null = null

const noise = () => (field ??= buildField())

// Built on the first call (~50 ms on a desktop, one-off), then cached.
export function smokeSprites(): HTMLCanvasElement[] {
  if (!sprites) {
    const smoke = tokenRgb('--foreground')
    const tint = tokenRgb('--primary')
    sprites = Array.from({ length: SPRITES }, (_, i) => renderSprite(noise(), i, smoke, tint))
  }
  return sprites
}

const toUrl = (c: HTMLCanvasElement) =>
  new Promise<string>((resolve, reject) =>
    c.toBlob((b) => (b ? resolve(URL.createObjectURL(b)) : reject(new Error('toBlob')))),
  )

// The divider rims' tiles as object URLs: the violet- and rose-tinted smoke (CSS background-image)
// and the reach mask (CSS mask-image). toBlob encodes off the main thread; the URLs live as long as
// the page.
export function smokeTileUrls(): Promise<[string, string, string]> {
  if (!tiles) {
    const smoke = tokenRgb('--foreground')
    const violet = renderTile(noise(), OFFSET, smoke, tokenRgb('--secondary'))
    const rose = renderTile(noise(), OFFSET + 83, smoke, tokenRgb('--accent'))
    const reachMask = renderReach(noise(), OFFSET + 151)
    tiles = Promise.all([toUrl(violet), toUrl(rose), toUrl(reachMask)])
  }
  return tiles
}

// Per-divider rim motion, set once from JS (§4.3 "Randomness"): flow slant, speed, tile sizes
// (square, so the texture keeps 1:1), texture offset, phase (negative delay), sway and the ragged
// reach's noise mask differ per layer, strip, divider and load, so the two dividers never move in
// sync. Consumed by .divider-smoke in main.css.

// The reach mask is two layers of the reach tile, intersected, at incommensurate square sizes
// (160–240 px and 1.35–1.65× that): large, so one column's reach barely varies across the rim's
// height, and two sizes, so the combined ragged edge does not repeat along the line. Per cycle each
// layer drifts one of its tiles outward and one sideways, in opposite sideways directions, at ~4–8
// px/s, so the edge changes shape in place rather than sliding.
function reach(side: 'up' | 'down'): Record<string, string> {
  const s = rand(160, 240)
  const s2 = s * rand(1.35, 1.65)
  const dur = s / rand(4, 8)
  const dir = Math.random() < 0.5 ? 1 : -1
  return {
    [`--rim-${side}-reach-s`]: `${Math.round(s)}px`,
    [`--rim-${side}-reach-s2`]: `${Math.round(s2)}px`,
    [`--rim-${side}-reach-x`]: `${Math.round(rand(0, s))}px`,
    [`--rim-${side}-reach-y`]: `${Math.round(rand(0, s))}px`,
    [`--rim-${side}-reach-x2`]: `${Math.round(rand(0, s2))}px`,
    [`--rim-${side}-reach-y2`]: `${Math.round(rand(0, s2))}px`,
    [`--rim-${side}-reach-dir`]: String(dir),
    [`--rim-${side}-reach-dur`]: `${dur.toFixed(1)}s`,
    [`--rim-${side}-reach-delay`]: `${(-rand(0, dur)).toFixed(1)}s`,
  }
}

// Flow slants as whole tiles per cycle (across, outward), so every loop is seamless on both axes:
// ≈ 18°, 27° and 34° off vertical (owner: "never straight up or down", §4.3).
const SLANTS: [number, number][] = [
  [1, 3],
  [1, 2],
  [2, 3],
]

// One strip (the half above or below the line): its two smoke layers drift outward at two different
// slants, each leaning left or right at random (so they cross or fan out), at ~12–22 px/s outward;
// the strip's sway wrapper swings ±5–10 px sideways (eased, alternate, 3–5.5 s per swing), so the
// direction also wanders over time.
function flow(side: 'up' | 'down', sizes: [number, number]): Record<string, string> {
  const first = Math.floor(Math.random() * SLANTS.length)
  const second = (first + 1 + Math.floor(Math.random() * (SLANTS.length - 1))) % SLANTS.length
  const out: Record<string, string> = {}
  for (const [n, k] of [first, second].entries()) {
    const [across, outward] = SLANTS[k]!
    const size = sizes[n]!
    const lean = Math.random() < 0.5 ? 1 : -1
    const dur = (outward * size) / rand(12, 22)
    const key = `--rim-${side}-${n + 1}`
    out[`${key}-tx`] = `${lean * across * size}px`
    out[`${key}-ty`] = `${outward * size}px`
    out[`${key}-ox`] = `${across * size}px` // horizontal oversize, so the drift never shows an edge
    out[`${key}-dur`] = `${dur.toFixed(2)}s`
    out[`${key}-delay`] = `${(-rand(0, dur)).toFixed(2)}s`
  }
  const sway = rand(3, 5.5)
  out[`--rim-${side}-sway`] = `${(rand(5, 10) * (Math.random() < 0.5 ? 1 : -1)).toFixed(1)}px`
  out[`--rim-${side}-sway-dur`] = `${sway.toFixed(2)}s`
  out[`--rim-${side}-sway-delay`] = `${(-rand(0, 2 * sway)).toFixed(2)}s`
  return out
}

export function smokeRimStyle(): Record<string, string> {
  const s = Math.round(rand(100, 150))
  // second layer: an incommensurate size, so the sum never repeats visibly
  const s2 = Math.round(s * rand(1.3, 1.6))
  return {
    '--rim-s': `${s}px`,
    '--rim-s2': `${s2}px`,
    '--rim-up-x': `${Math.round(rand(0, 120))}px`,
    '--rim-down-x': `${Math.round(rand(0, 120))}px`,
    ...flow('up', [s, s2]),
    ...flow('down', [s, s2]),
    // ragged reach (noise mask): size, offset and slow drift, independent per strip
    ...reach('up'),
    ...reach('down'),
  }
}
