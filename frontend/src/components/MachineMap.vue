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
  'block size-4 rounded-full border-2 border-foreground bg-primary shadow-[0_0_12px_4px_var(--glow)]'
const DOT_SELECTED =
  'block size-6 rounded-full border-2 border-foreground bg-accent shadow-[0_0_18px_6px_var(--glow)] ring-4 ring-accent/40'

function icon(selected: boolean) {
  return L.divIcon({
    className: 'machine-marker',
    html: `<span class="grid size-full place-items-center"><span class="${selected ? DOT_SELECTED : DOT}"></span></span>`,
    iconSize: [32, 32],
    iconAnchor: [16, 16],
  })
}

function renderMarkers() {
  if (!map || !layer) return
  layer.clearLayers()
  markers.clear()
  for (const m of props.machines) {
    const selected = m.id === props.selectedId
    const marker = L.marker([m.lat, m.lng], {
      icon: icon(selected),
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
})

onUnmounted(() => {
  map?.remove()
  map = null
  layer = null
  markers.clear()
})

watch(() => props.machines, renderMarkers)

watch(
  () => props.selectedId,
  (id, prev) => {
    for (const key of [prev, id]) {
      if (key == null) continue
      const marker = markers.get(key)
      if (!marker) continue
      const selected = key === id
      marker.setIcon(icon(selected)).setZIndexOffset(selected ? 1000 : 0)
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
