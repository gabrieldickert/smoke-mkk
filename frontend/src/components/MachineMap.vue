<script lang="ts">
// Module scope (not per instance): the marker drop plays once per page load, not on every
// return to "/".
let droppedThisLoad = false
</script>

<script setup lang="ts">
import L from 'leaflet'
import { onMounted, onUnmounted, ref, watch } from 'vue'
import type { Machine } from '../api'

const props = defineProps<{ machines: Machine[]; selectedId: number | null }>()
const emit = defineEmits<{ select: [id: number] }>()

const el = ref<HTMLDivElement | null>(null)
let map: L.Map | null = null
let layer: L.LayerGroup | null = null
const markers = new Map<number, L.Marker>()

// Main-Kinzig area; shown until the machines are loaded (or when they fail to load).
const FALLBACK_CENTER: L.LatLngExpression = [50.3, 9.3]

const DOT =
  'marker-dot block size-4 rounded-full border-2 border-foreground bg-secondary shadow-[0_0_14px_5px_var(--glow)]'
const DOT_SELECTED =
  'marker-dot block size-6 rounded-full border-2 border-foreground bg-accent shadow-[0_0_18px_6px_var(--glow)] ring-4 ring-accent/40'

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

function icon(selected: boolean, delayMs = 0, at: Phase = phase) {
  const wrap = at === 'wait' ? 'marker-wait' : at === 'drop' ? 'marker-drop' : ''
  return L.divIcon({
    className: 'machine-marker',
    html: `<span class="${wrap} relative grid size-full place-items-center" style="--drop-delay:${delayMs}ms"><span class="${selected ? DOT_SELECTED : DOT}"></span></span>`,
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
  for (const [id, marker] of markers) marker.setIcon(icon(id === props.selectedId, delays.get(id)))
  droppedThisLoad = true
  doneTimer = window.setTimeout(() => (phase = 'done'), DROP_TOTAL_MS)
}

function renderMarkers() {
  if (!map || !layer) return
  layer.clearLayers()
  markers.clear()
  const delays = dropDelays()
  for (const m of props.machines) {
    const selected = m.id === props.selectedId
    const marker = L.marker([m.lat, m.lng], {
      icon: icon(selected, delays.get(m.id)),
      title: m.name,
      keyboard: true,
      zIndexOffset: selected ? 1000 : 0,
    })
      .on('click', () => emit('select', m.id))
      .addTo(layer)
    const node = marker.getElement()
    node?.setAttribute('aria-label', m.name)
    node?.setAttribute('aria-pressed', String(selected))
    markers.set(m.id, marker)
  }
  if (props.machines.length) {
    map.fitBounds(L.latLngBounds(props.machines.map((m) => [m.lat, m.lng])), {
      padding: [40, 40],
      maxZoom: 13,
    })
  }
}

onMounted(() => {
  if (!el.value) return
  map = L.map(el.value, { scrollWheelZoom: false }).setView(FALLBACK_CENTER, 9)
  L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19,
    attribution:
      '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>-Mitwirkende',
  }).addTo(map)
  layer = L.layerGroup().addTo(map)
  renderMarkers()
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
    for (const key of [prev, id]) {
      if (key == null) continue
      const marker = markers.get(key)
      if (!marker) continue
      const selected = key === id
      // A selection never re-plays the drop; it keeps the marker hidden only before the reveal.
      marker.setIcon(icon(selected, 0, phase === 'wait' ? 'wait' : 'done')).setZIndexOffset(selected ? 1000 : 0)
      marker.getElement()?.setAttribute('aria-pressed', String(selected))
    }
    const target = id == null ? undefined : markers.get(id)
    if (map && target && !map.getBounds().contains(target.getLatLng())) {
      map.panTo(target.getLatLng())
    }
  },
)
</script>

<template>
  <div ref="el" class="map-dark size-full" />
</template>
