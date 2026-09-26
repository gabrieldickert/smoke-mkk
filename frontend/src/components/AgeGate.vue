<script setup lang="ts">
import { nextTick, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'

const FLAG = 'ageConfirmed'
// Legal pages stay reachable without confirming (CLAUDE.md §5.5, docs/PLAN.md §4.3 F10).
const OPEN_ROUTES = new Set(['impressum', 'datenschutz'])

// "closed": the gate is settled and shut (confirmed, a legal page, or "Ja"). App.vue lets the
// smoke intro dissolve on it (docs/PLAN.md §4.3 "Smoke intro"). Repeats are harmless.
const emit = defineEmits<{ closed: [] }>()

const route = useRoute()
const router = useRouter()
const dialog = ref<HTMLDialogElement | null>(null)
const deniedText = ref<HTMLParagraphElement | null>(null)
const denied = ref(false)
let confirmed = isConfirmed()
let ready = false // route.name is START_LOCATION's (undefined) until the first navigation settles

function isConfirmed(): boolean {
  try {
    return localStorage.getItem(FLAG) === '1'
  } catch {
    return false
  }
}

// Single source of truth for open/closed. Also runs on `close`: Chrome lets a second Escape
// through even when `cancel` is prevented, so an unconfirmed close outside the legal pages reopens.
function sync() {
  const d = dialog.value
  if (!d || !ready) return
  const shouldOpen = !confirmed && !OPEN_ROUTES.has(String(route.name))
  if (shouldOpen && !d.open) d.showModal()
  else if (!shouldOpen && d.open) d.close()
  if (!d.open) emit('closed')
}

onMounted(async () => {
  await router.isReady()
  ready = true
  sync()
})

watch(() => route.name, sync)

function yes() {
  confirmed = true
  try {
    localStorage.setItem(FLAG, '1')
  } catch {
    // storage blocked (private mode): the gate simply shows again next visit
  }
  sync()
}

async function no() {
  denied.value = true
  await nextTick()
  deniedText.value?.focus()
}

const legalLink =
  'inline-flex min-h-11 items-center rounded-md px-3 text-sm text-muted-foreground transition-colors duration-200 hover:text-foreground'
</script>

<template>
  <dialog
    ref="dialog"
    closedby="none"
    :aria-labelledby="denied ? 'age-denied' : 'age-title'"
    :aria-describedby="denied ? undefined : 'age-body'"
    class="age-gate m-auto w-[min(28rem,calc(100vw-2rem))] rounded-2xl border border-border bg-card p-6 text-card-foreground shadow-[0_0_48px_var(--glow)] backdrop:bg-background/90 backdrop:backdrop-blur-sm sm:p-8"
    @cancel.prevent
    @close="sync"
  >
    <p
      v-if="denied"
      id="age-denied"
      ref="deniedText"
      tabindex="-1"
      class="text-center text-lg font-medium"
    >
      Dann ist die Seite noch nichts für dich.
    </p>
    <template v-else>
      <img src="/logo.webp" alt="" width="80" height="80" class="mx-auto mb-5 size-20 rounded-xl" />
      <h2 id="age-title" class="text-center text-2xl font-bold">Bist du 18 oder älter?</h2>
      <p id="age-body" class="mt-3 text-center text-muted-foreground">
        Hier geht's auch um Vapes und Tabak. Die gibt's erst ab 18.
      </p>
      <div class="mt-6 flex flex-col gap-3 sm:flex-row-reverse">
        <button
          type="button"
          autofocus
          class="min-h-11 min-w-0 flex-1 cursor-pointer rounded-xl bg-primary px-5 py-2.5 font-semibold text-primary-foreground transition-colors duration-200 hover:bg-primary/85"
          @click="yes"
        >
          Ja, ich bin 18+
        </button>
        <button
          type="button"
          class="min-h-11 min-w-0 flex-1 cursor-pointer rounded-xl border border-border px-5 py-2.5 font-semibold transition-colors duration-200 hover:bg-muted"
          @click="no"
        >
          Nein, noch nicht
        </button>
      </div>
    </template>
    <!-- Both states: the legal pages must stay reachable from the gate. Navigating there closes it
         (sync on route.name). -->
    <div class="mt-5 flex justify-center gap-2">
      <RouterLink to="/impressum" :class="legalLink">Impressum</RouterLink>
      <RouterLink to="/datenschutz" :class="legalLink">Datenschutz</RouterLink>
    </div>
  </dialog>
</template>
