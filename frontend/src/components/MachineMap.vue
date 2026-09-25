<script lang="ts">
// Module scope (not per instance): the marker drop plays once per page load, not on every
// return to "/".
let droppedThisLoad = false
</script>

<script setup lang="ts">
import L from 'leaflet'
import { onMounted, onUnmounted, ref, watch } from 'vue'
import { placeName, type Machine } from '../api'

const props = defineProps<{
  machines: Machine[]
  selectedId: number | null
  position: Pick<Machine, 'lat' | 'lng'> | null // the visitor, once known (memory only)
  nearestId: number | null
}>()
const emit = defineEmits<{ select: [id: number] }>()

const el = ref<HTMLDivElement | null>(null)
let map: L.Map | null = null
let layer: L.LayerGroup | null = null
const markers = new Map<number, L.Marker>()
let you: L.Marker | null = null

// Main-Kinzig area; shown until the machines are loaded (or when they fail to load).
const FALLBACK_CENTER: L.LatLngExpression = [50.3, 9.3]
// Selecting zooms in at least this far, so neighbours that overlap at the start zoom separate
// (Hesseldorf and Wächtersbach are ~5 px apart at 375 px wide).
const SELECT_ZOOM = 12

const reducedMotion = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches

const DOT =
  'marker-dot block size-4 rounded-full border-2 border-foreground bg-secondary shadow-[0_0_14px_5px_var(--glow)]'
const DOT_SELECTED =
  'marker-dot block size-6 rounded-full border-2 border-foreground bg-accent shadow-[0_0_18px_6px_var(--glow)] ring-4 ring-accent/40'
// Nearest machine: a persistent dashed ring outside the dot (and outside the selected ring), static,
// so it reads as "marked" without competing with the selection. Inside the drop wrapper, so it
// drops in with its marker.
const NEAREST_RING =
  '<span class="pointer-events-none absolute -inset-1.5 rounded-full border-2 border-dashed border-foreground"></span>'
// Visitor: small foreground core in a primary ring, clearly not a machine.
const YOU_DOT =
  '<span class="block size-3.5 rounded-full border-[3px] border-primary bg-foreground shadow-[0_0_10px_3px_var(--glow)]"></span>'

// Marker drop-in (docs/PLAN.md §4.3): hidden until the map is on screen, then dropped
// west→east, once per page load. Only the inner wrapper animates — Leaflet owns the
// icon element's transform. Timing in main.css (.marker-drop).
const DROP_STAGGER_MS = 50 // 10 × 50 + 450 ms keeps 11 markers under 1 s
const DROP_TOTAL_MS = 1100 // drop + landing ring of the last marker
type Phase = 'wait' | 'drop' | 'done'
let phase: Phase = droppedThisLoad ? 'done' : 'wait'
let observer: IntersectionObserver | null = null
let doneTimer: number | undefined
let visible = false // map has been on screen; machines arriving later drop in at once

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

// Stagger index per machine, west→east.
function dropDelays(): Map<number, number> {
  return new Map(
    [...props.machines].sort((a, b) => a.lng - b.lng).map((m, i) => [m.id, i * DROP_STAGGER_MS]),
  )
}

function startDrop() {
  if (phase !== 'wait' || !markers.size) return
  phase = 'drop'
  const delays = dropDelays()
  for (const [id, marker] of markers) marker.setIcon(icon(id, delays.get(id)))
  droppedThisLoad = true
  doneTimer = window.setTimeout(() => (phase = 'done'), DROP_TOTAL_MS)
}

const label = (m: Machine) => (m.id === props.nearestId ? `${placeName(m)} · Am nächsten` : placeName(m))

// Re-applies selected/nearest state to one marker. A state change never re-plays the drop; it
// keeps the marker hidden only before the reveal.
function refresh(id: number | null | undefined) {
  const marker = id == null ? undefined : markers.get(id)
  const m = props.machines.find((x) => x.id === id)
  if (!marker || !m) return
  const selected = m.id === props.selectedId
  marker
    .setIcon(icon(m.id, 0, phase === 'wait' ? 'wait' : 'done'))
    .setZIndexOffset(selected ? 1000 : 0)
    .setTooltipContent(label(m))
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
    // Hover shows the tooltip; no click handler, not in the tab order, below the machine markers.
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
  for (const m of props.machines) {
    const selected = m.id === props.selectedId
    // No `title`: it would double the tooltip. Leaflet 1.9 opens the tooltip on hover and on
    // keyboard focus; aria-label names the marker for screen readers.
    const marker = L.marker([m.lat, m.lng], {
      icon: icon(m.id, delays.get(m.id)),
      keyboard: true,
      zIndexOffset: selected ? 1000 : 0,
    })
      .bindTooltip(label(m), { direction: 'top', offset: [0, -14] })
      .on('click', () => emit('select', m.id))
      .addTo(layer)
    const node = marker.getElement()
    node?.setAttribute('aria-label', placeName(m))
    node?.setAttribute('aria-pressed', String(selected))
    markers.set(m.id, marker)
  }
  fitAll(false)
}

function fitAll(animate: boolean) {
  if (!map || !props.machines.length) return
  map.fitBounds(L.latLngBounds(props.machines.map((m) => [m.lat, m.lng])), {
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
  () => props.machines,
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
  <div ref="el" class="map-dark size-full" />
</template>
