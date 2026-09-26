<script lang="ts">
// Module scope (not per instance): the marker drop plays once per page load, not on every
// return to "/".
let droppedThisLoad = false
</script>

<script setup lang="ts">
import L from 'leaflet'
import { onMounted, onUnmounted, ref, watch } from 'vue'
import { placeName, type Location } from '../api'

// One marker per location (docs/PLAN.md §5, B5); ids here are location ids. Where locations
// overlap at the current zoom they share one count pin (F23).
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
// Rendered pins, keyed by their member ids ("7" for a single location, "3,7,9" for a group), so a
// pin that survives a regroup keeps its element (focus, tooltip) and only changes when its state
// does. `sig` is the icon's state; the icon is replaced only when it changes.
const pins = new Map<string, { marker: L.Marker; sig: string }>()
let you: L.Marker | null = null

// Main-Kinzig area; shown until the locations are loaded (or when they fail to load).
const FALLBACK_CENTER: L.LatLngExpression = [50.3, 9.3]
// Select zoom (docs/PLAN.md §4.3 "UX audit 2" 1): the smallest zoom in this range at which the
// selected pin's nearest neighbour is ≥ SELECT_GAP px away (Bad Orb's sites need 15).
const SELECT_ZOOM_MIN = 12
const SELECT_ZOOM_MAX = 16
const SELECT_GAP = 40
// Grouping: pins closer than GROUP_GAP px at the current zoom share a count pin; from
// GROUP_OFF_ZOOM up every location stands alone (nothing in §3 overlaps there).
const GROUP_GAP = 28
const GROUP_OFF_ZOOM = 16

const reducedMotion = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches

// Pins on the lighter map (F18): deep fills (≥ 3:1 on the land), light stroke, neon glow in the
// bright brand hue. Tokens in main.css (.dark: --marker*).
const DOT =
  'marker-dot block size-4 rounded-full border-2 border-foreground bg-marker shadow-[0_0_12px_4px_var(--marker-glow)]'
const DOT_SELECTED =
  'marker-dot block size-6 rounded-full border-2 border-foreground bg-marker-selected shadow-[0_0_14px_5px_var(--marker-selected-glow)] ring-4 ring-accent/50'
// Group pin (F23): the idle dot enlarged to 28 px with §6 map.clusterCount "{n}x" inside (white on
// --marker ≈ 11:1, on --marker-selected ≈ 10:1); two-digit counts widen it into a pill. It takes
// the selected look when the selection is one of its members.
const GROUP =
  'marker-dot grid h-7 min-w-7 place-items-center rounded-full border-2 border-foreground px-1 text-xs font-bold text-primary-foreground tabular-nums'
const GROUP_IDLE = `${GROUP} bg-marker shadow-[0_0_12px_4px_var(--marker-glow)]`
const GROUP_SELECTED = `${GROUP} bg-marker-selected shadow-[0_0_14px_5px_var(--marker-selected-glow)] ring-4 ring-accent/50`
// Nearest location: a persistent dashed ring outside the dot (and outside the selected ring), static,
// so it reads as "marked" without competing with the selection. Inside the drop wrapper, so it
// drops in with its marker. On a group it marks the group holding the nearest location.
const NEAREST_RING =
  '<span class="pointer-events-none absolute -inset-1.5 rounded-full border-2 border-dashed border-marker"></span>'
// Visitor: small foreground core in a deep violet ring (the pins inverted), clearly not a location.
const YOU_DOT =
  '<span class="block size-3.5 rounded-full border-[3px] border-marker bg-foreground shadow-[0_0_10px_3px_var(--marker-glow)]"></span>'

// Marker drop-in (docs/PLAN.md §4.3): hidden until the map is on screen, then dropped
// west→east, once per page load, over whatever is rendered then (group pins included); a regroup
// after a zoom never replays it. Only the inner wrapper animates — Leaflet owns the icon element's
// transform. Timing in main.css (.marker-drop).
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

type Pin = { key: string; members: Location[]; at: L.LatLng }

// ponytail: O(n²) pair scan per merge, run on every render and zoomend; fine below ~200 pins.
// Past that, bucket the projected points in a 28 px grid first.
// Grouping merges the closest pair of pins/groups while it is < 28 px apart (the merged group sits
// at its members' centroid), so every rendered pin keeps ≥ 28 px to the next. Not the plan's
// first-fit west→east walk: that chains through growing centroids (at zoom 9 Wächtersbach joined
// Bad Orb's five) and can leave two centroids < 28 px apart.
function group(): Pin[] {
  if (!map) return []
  const z = map.getZoom()
  const pts = [...props.locations]
    .sort((a, b) => a.lng - b.lng)
    .map((l) => ({ members: [l], p: map!.project([l.lat, l.lng], z) }))
  while (z < GROUP_OFF_ZOOM) {
    let best: [number, number] | null = null
    let bestD = GROUP_GAP
    for (let i = 0; i < pts.length; i++)
      for (let j = i + 1; j < pts.length; j++) {
        const d = pts[i]!.p.distanceTo(pts[j]!.p)
        if (d < bestD) [best, bestD] = [[i, j], d]
      }
    if (!best) break
    const a = pts[best[0]]!
    const [b] = pts.splice(best[1], 1)
    const n = a.members.length
    const m = b!.members.length
    a.p = a.p.multiplyBy(n).add(b!.p.multiplyBy(m)).divideBy(n + m)
    a.members.push(...b!.members)
  }
  return pts.map(({ members, p }) => ({
    key: members.sort((a, b) => a.lng - b.lng).map((l) => l.id).join(','),
    members,
    at: map!.unproject(p, z),
  }))
}

// §6 single-pin tooltip line 1: display name, then "· {n} Automaten" at 2+ machines, then
// "· Am nächsten".
const title = (l: Location) =>
  [
    placeName(l),
    l.machines.length > 1 && `${l.machines.length} Automaten`,
    l.id === props.nearestId && 'Am nächsten',
  ]
    .filter(Boolean)
    .join(' · ')
// aria-label. Single: line 1, then " · " + §6 map.pinAddress (street with house number, as stored;
// omitted when ""). Group: §6 map.cluster "{n} Standorte: name, name, …".
function label(pin: Pin) {
  const ls = pin.members
  if (ls.length > 1) return `${ls.length} Standorte: ${ls.map(placeName).join(', ')}`
  return [title(ls[0]!), ls[0]!.street].filter(Boolean).join(' · ')
}
// Tooltip (HTML, names escaped). Single: line 1, then the street as a muted smaller line. Group:
// "{n} Standorte" in bold, then one member per muted line (main.css .tip-line, .group-tip).
const esc = (s: string) => s.replace(/[&<>"']/g, (c) => `&#${c.charCodeAt(0)};`)
const line = (s: string) => (s ? `<span class="tip-line">${esc(s)}</span>` : '')
function tooltip(pin: Pin) {
  const ls = pin.members
  if (ls.length > 1)
    return `<strong>${ls.length} Standorte</strong>${ls.map((l) => line(placeName(l))).join('')}`
  return esc(title(ls[0]!)) + line(ls[0]!.street)
}

const isSelected = (pin: Pin) => pin.members.some((l) => l.id === props.selectedId)

function inner(pin: Pin) {
  const ring = pin.members.some((l) => l.id === props.nearestId) ? NEAREST_RING : ''
  const sel = isSelected(pin)
  const dot =
    pin.members.length > 1
      ? `<span class="${sel ? GROUP_SELECTED : GROUP_IDLE}">${pin.members.length}x</span>`
      : `<span class="${sel ? DOT_SELECTED : DOT}"></span>`
  return ring + dot
}

function icon(html: string, at: Phase, delayMs: number) {
  const wrap = at === 'wait' ? 'marker-wait' : at === 'drop' ? 'marker-drop' : ''
  return L.divIcon({
    className: 'machine-marker',
    html: `<span class="${wrap} relative grid size-full place-items-center" style="--drop-delay:${delayMs}ms">${html}</span>`,
    iconSize: [32, 32],
    iconAnchor: [16, 16],
  })
}

// The one render path: regroups at the current zoom and reconciles the rendered pins with the
// result. Called on load, on every zoomend (never during the zoom animation), on selected/nearest
// changes and, with `drop`, once at the reveal. Before the reveal every pin stays hidden; after it
// a changed or new pin appears without replaying the drop.
function render(drop = false) {
  if (!map || !layer) return
  const next = group()
  const step = dropStagger(next.length)
  const order = drop ? [...next].sort((a, b) => a.at.lng - b.at.lng).map((p) => p.key) : []
  // A keyboard user who opens a group loses its element; focus moves to the pin that now holds
  // the group's first member, so the next Tab continues among the members.
  const active = document.activeElement
  let refocus: number | undefined
  for (const [key, { marker }] of pins) {
    if (next.some((p) => p.key === key)) continue
    const node = marker.getElement()
    if (node && node === active && node.matches(':focus-visible')) refocus = Number(key.split(',')[0])
    marker.remove()
    pins.delete(key)
  }
  const at: Phase = drop ? 'drop' : phase === 'wait' ? 'wait' : 'done'
  for (const pin of next) {
    const html = inner(pin)
    const text = label(pin)
    const tip = tooltip(pin)
    const selected = isSelected(pin)
    const nextIcon = () => icon(html, at, drop ? Math.round(order.indexOf(pin.key) * step) : 0)
    let entry = pins.get(pin.key)
    if (!entry) {
      const members = pin.members
      // A group zooms until its members separate (or split at the next zoomend); a pin selects.
      const activate = () =>
        members.length > 1
          ? map?.fitBounds(L.latLngBounds(members.map((l) => [l.lat, l.lng])), {
              padding: [40, 40],
              maxZoom: GROUP_OFF_ZOOM,
              animate: !reducedMotion(),
            })
          : emit('select', members[0]!.id)
      // No `title`: it would double the tooltip. Leaflet 1.9 opens the tooltip on hover and on
      // keyboard focus and sets role="button" + tabindex 0, but never activates a marker from the
      // keyboard; Enter and Space do here, as on a native button.
      const marker = L.marker(pin.at, { icon: nextIcon(), keyboard: true })
        .bindTooltip(tip, {
          direction: 'top',
          offset: [0, members.length > 1 ? -18 : -14],
          className: members.length > 1 ? 'group-tip' : '',
        })
        .on('click', activate)
        .on('keydown', ({ originalEvent: e }) => {
          if ((e as KeyboardEvent).key !== 'Enter' && (e as KeyboardEvent).key !== ' ') return
          e.preventDefault()
          activate()
        })
        .addTo(layer)
      entry = { marker, sig: html + tip }
      pins.set(pin.key, entry)
    } else {
      if (!entry.marker.getLatLng().equals(pin.at)) entry.marker.setLatLng(pin.at)
      if (drop || entry.sig !== html + tip) {
        entry.marker.setIcon(nextIcon()).setTooltipContent(tip)
        entry.sig = html + tip
      }
    }
    entry.marker.setZIndexOffset(selected ? 1000 : 0)
    const node = entry.marker.getElement()
    node?.setAttribute('aria-label', text)
    if (pin.members.length === 1) node?.setAttribute('aria-pressed', String(selected))
  }
  const target = next.find((p) => p.members.some((l) => l.id === refocus))
  if (target) pins.get(target.key)?.marker.getElement()?.focus()
}

function startDrop() {
  if (phase !== 'wait' || !pins.size) return
  phase = 'drop'
  render(true)
  droppedThisLoad = true
  doneTimer = window.setTimeout(
    () => (phase = 'done'),
    Math.round((pins.size - 1) * dropStagger(pins.size)) + DROP_LAST_MS,
  )
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
    // Never grouped.
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

function fitAll(animate: boolean) {
  if (!map || !props.locations.length) return
  map.fitBounds(L.latLngBounds(props.locations.map((l) => [l.lat, l.lng])), {
    padding: [40, 40],
    maxZoom: 13,
    animate,
  })
}

// Smallest zoom in 12…16 where the selected pin's nearest other location is ≥ 40 px away
// (16 if none), never below the current zoom.
function selectZoom(target: Location) {
  if (!map) return SELECT_ZOOM_MIN
  const others = props.locations.filter((l) => l.id !== target.id)
  let z = SELECT_ZOOM_MIN
  for (; z < SELECT_ZOOM_MAX; z++) {
    const p = map.project([target.lat, target.lng], z)
    if (others.every((l) => p.distanceTo(map!.project([l.lat, l.lng], z)) >= SELECT_GAP)) break
  }
  return Math.max(map.getZoom(), z)
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
  map.on('zoomend', () => render())
  fitAll(false)
  render()
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
  pins.clear()
})

watch(
  () => props.locations,
  () => {
    layer?.clearLayers()
    pins.clear()
    fitAll(false)
    render()
    if (visible) startDrop()
  },
)

watch(
  () => props.selectedId,
  (id) => {
    render()
    if (!map) return
    const target = props.locations.find((l) => l.id === id)
    if (target) {
      map.setView([target.lat, target.lng], selectZoom(target), { animate: !reducedMotion() })
    } else if (id == null) {
      fitAll(!reducedMotion())
    }
  },
)

watch(() => props.nearestId, () => render())

watch(() => props.position, placeYou)
</script>

<template>
  <div ref="el" class="map-brand size-full" />
</template>
