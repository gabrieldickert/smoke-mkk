<script lang="ts">
// Module scope (not per instance): the marker drop plays once per page load, not on every
// return to "/".
let droppedThisLoad = false
</script>

<script setup lang="ts">
import L from 'leaflet'
import { onMounted, onUnmounted, ref, watch } from 'vue'
import { placeName, type Location } from '../api'

// One marker per location (docs/PLAN.md §5, B5); ids here are location ids.
const props = defineProps<{
  locations: Location[]
  selectedId: number | null
  position: Pick<Location, 'lat' | 'lng'> | null // the visitor, once known (memory only)
  nearestId: number | null
}>()
const emit = defineEmits<{ select: [id: number] }>()

const el = ref<HTMLDivElement | null>(null)
let map: L.Map | null = null
let layer: L.LayerGroup | null = null
const markers = new Map<number, L.Marker>()
let you: L.Marker | null = null

// Main-Kinzig area; shown until the locations are loaded (or when they fail to load).
const FALLBACK_CENTER: L.LatLngExpression = [50.3, 9.3]
// Selecting zooms in at least this far, so neighbours that overlap at the start zoom separate
// (Hesseldorf and Wächtersbach are ~5 px apart at 375 px wide).
const SELECT_ZOOM = 12

const reducedMotion = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches

// Pins on the lighter map (F18): deep fills (≥ 3:1 on the land), light stroke, neon glow in the
// bright brand hue. Tokens in main.css (.dark: --marker*).
const DOT =
  'marker-dot block size-4 rounded-full border-2 border-foreground bg-marker shadow-[0_0_12px_4px_var(--marker-glow)]'
const DOT_SELECTED =
  'marker-dot block size-6 rounded-full border-2 border-foreground bg-marker-selected shadow-[0_0_14px_5px_var(--marker-selected-glow)] ring-4 ring-accent/50'
// Nearest location: a persistent dashed ring outside the dot (and outside the selected ring), static,
// so it reads as "marked" without competing with the selection. Inside the drop wrapper, so it
// drops in with its marker.
const NEAREST_RING =
  '<span class="pointer-events-none absolute -inset-1.5 rounded-full border-2 border-dashed border-marker"></span>'
// Visitor: small foreground core in a deep violet ring (the pins inverted), clearly not a location.
const YOU_DOT =
  '<span class="block size-3.5 rounded-full border-[3px] border-marker bg-foreground shadow-[0_0_10px_3px_var(--marker-glow)]"></span>'

// Marker drop-in (docs/PLAN.md §4.3): hidden until the map is on screen, then dropped
// west→east, once per page load. Only the inner wrapper animates — Leaflet owns the
// icon element's transform. Timing in main.css (.marker-drop).
// Stagger min(50, 500 / (n − 1)) ms (§4.3, F15): the last marker starts by 500 ms and lands by
// ~950 ms whatever the count; small counts keep the 50 ms rhythm.
const DROP_STAGGER_MAX_MS = 50
const DROP_SPREAD_MS = 500
const DROP_LAST_MS = 600 // drop + landing ring of the last marker, after its stagger delay
const dropStagger = (n: number) => (n > 1 ? Math.min(DROP_STAGGER_MAX_MS, DROP_SPREAD_MS / (n - 1)) : 0)
type Phase = 'wait' | 'drop' | 'done'
let phase: Phase = droppedThisLoad ? 'done' : 'wait'
let observer: IntersectionObserver | null = null
let doneTimer: number | undefined
let visible = false // map has been on screen; locations arriving later drop in at once

function icon(id: number, delayMs = 0, at: Phase = phase) {
  const wrap = at === 'wait' ? 'marker-wait' : at === 'drop' ? 'marker-drop' : ''
  const ring = id === props.nearestId ? NEAREST_RING : ''
  return L.divIcon({
    className: 'machine-marker',
    html: `<span class="${wrap} relative grid size-full place-items-center" style="--drop-delay:${delayMs}ms">${ring}<span class="${id === props.selectedId ? DOT_SELECTED : DOT}"></span></span>`,
    iconSize: [32, 32],
    iconAnchor: [16, 16],
  })
}

// Stagger delay per location, west→east.
function dropDelays(): Map<number, number> {
  const step = dropStagger(props.locations.length)
  return new Map(
    [...props.locations].sort((a, b) => a.lng - b.lng).map((l, i) => [l.id, Math.round(i * step)]),
  )
}

function startDrop() {
  if (phase !== 'wait' || !markers.size) return
  phase = 'drop'
  const delays = dropDelays()
  for (const [id, marker] of markers) marker.setIcon(icon(id, delays.get(id)))
  droppedThisLoad = true
  doneTimer = window.setTimeout(
    () => (phase = 'done'),
    Math.max(0, ...delays.values()) + DROP_LAST_MS,
  )
}

// §6 tooltip: display name, then "· {n} Automaten" at 2+ machines, then "· Am nächsten". The same
// text names the marker for screen readers.
const label = (l: Location) =>
  [
    placeName(l),
    l.machines.length > 1 && `${l.machines.length} Automaten`,
    l.id === props.nearestId && 'Am nächsten',
  ]
    .filter(Boolean)
    .join(' · ')

// Re-applies selected/nearest state to one marker. A state change never re-plays the drop; it
// keeps the marker hidden only before the reveal.
function refresh(id: number | null | undefined) {
  const marker = id == null ? undefined : markers.get(id)
  const l = props.locations.find((x) => x.id === id)
  if (!marker || !l) return
  const selected = l.id === props.selectedId
  marker
    .setIcon(icon(l.id, 0, phase === 'wait' ? 'wait' : 'done'))
    .setZIndexOffset(selected ? 1000 : 0)
    .setTooltipContent(label(l))
  marker.getElement()?.setAttribute('aria-label', label(l))
  marker.getElement()?.setAttribute('aria-pressed', String(selected))
}

function placeYou() {
  const p = props.position
  if (!map) return
  if (!p) {
    you?.remove()
    you = null
  } else if (you) {
    you.setLatLng([p.lat, p.lng])
  } else {
    // Hover shows the tooltip; no click handler, not in the tab order, below the location markers.
    you = L.marker([p.lat, p.lng], {
      icon: L.divIcon({ className: 'you-marker', html: YOU_DOT, iconSize: [20, 20], iconAnchor: [10, 10] }),
      keyboard: false,
      zIndexOffset: -1000,
    })
      .bindTooltip('Dein Standort', { direction: 'top', offset: [0, -8] })
      .addTo(map)
    you.getElement()?.setAttribute('role', 'img')
    you.getElement()?.setAttribute('aria-label', 'Dein Standort')
  }
}

function renderMarkers() {
  if (!map || !layer) return
  layer.clearLayers()
  markers.clear()
  const delays = dropDelays()
  for (const l of props.locations) {
    const selected = l.id === props.selectedId
    // No `title`: it would double the tooltip. Leaflet 1.9 opens the tooltip on hover and on
    // keyboard focus; aria-label names the marker for screen readers.
    const marker = L.marker([l.lat, l.lng], {
      icon: icon(l.id, delays.get(l.id)),
      keyboard: true,
      zIndexOffset: selected ? 1000 : 0,
    })
      .bindTooltip(label(l), { direction: 'top', offset: [0, -14] })
      .on('click', () => emit('select', l.id))
      .addTo(layer)
    const node = marker.getElement()
    node?.setAttribute('aria-label', label(l))
    node?.setAttribute('aria-pressed', String(selected))
    markers.set(l.id, marker)
  }
  fitAll(false)
}

function fitAll(animate: boolean) {
  if (!map || !props.locations.length) return
  map.fitBounds(L.latLngBounds(props.locations.map((l) => [l.lat, l.lng])), {
    padding: [40, 40],
    maxZoom: 13,
    animate,
  })
}

onMounted(() => {
  if (!el.value) return
  // Touch: one-finger drag starts off so a swipe over the map scrolls the page (no scroll trap);
  // pinch zoom stays. The first tap on the map background turns dragging on. Marker taps do not
  // reach the map's click, so they select without unlocking.
  const coarse = window.matchMedia('(pointer: coarse)').matches
  map = L.map(el.value, { scrollWheelZoom: false, dragging: !coarse }).setView(FALLBACK_CENTER, 9)
  if (coarse) map.once('click', () => map?.dragging.enable())
  L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19,
    attribution:
      '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>-Mitwirkende',
  }).addTo(map)
  layer = L.layerGroup().addTo(map)
  renderMarkers()
  placeYou()
  if (phase === 'wait') {
    observer = new IntersectionObserver(
      (entries) => {
        if (!entries.some((e) => e.isIntersecting)) return
        observer?.disconnect()
        observer = null
        visible = true
        startDrop()
      },
      { threshold: 0.3 },
    )
    observer.observe(el.value)
  }
})

onUnmounted(() => {
  observer?.disconnect()
  window.clearTimeout(doneTimer)
  map?.remove()
  map = null
  layer = null
  markers.clear()
})

watch(
  () => props.locations,
  () => {
    renderMarkers()
    if (visible) startDrop()
  },
)

watch(
  () => props.selectedId,
  (id, prev) => {
    refresh(prev)
    refresh(id)
    if (!map) return
    const target = id == null ? undefined : markers.get(id)
    if (target) {
      map.setView(target.getLatLng(), Math.max(map.getZoom(), SELECT_ZOOM), {
        animate: !reducedMotion(),
      })
    } else if (id == null) {
      fitAll(!reducedMotion())
    }
  },
)

watch(
  () => props.nearestId,
  (id, prev) => {
    refresh(prev)
    refresh(id)
  },
)

watch(() => props.position, placeYou)
</script>

<template>
  <div ref="el" class="map-brand size-full" />
</template>
