<script setup lang="ts">
import { nextTick, onMounted, ref } from 'vue'

const FLAG = 'ageConfirmed'

const dialog = ref<HTMLDialogElement | null>(null)
const deniedText = ref<HTMLParagraphElement | null>(null)
const denied = ref(false)
let confirmed = false

function isConfirmed(): boolean {
  try {
    return localStorage.getItem(FLAG) === '1'
  } catch {
    return false
  }
}

onMounted(() => {
  if (!isConfirmed()) dialog.value?.showModal()
})

function yes() {
  confirmed = true
  try {
    localStorage.setItem(FLAG, '1')
  } catch {
    // storage blocked (private mode): the gate simply shows again next visit
  }
  dialog.value?.close()
}

async function no() {
  denied.value = true
  await nextTick()
  deniedText.value?.focus()
}

// Chrome lets a second Escape through even when `cancel` is prevented; reopen.
function onClose() {
  if (!confirmed) dialog.value?.showModal()
}
</script>

<template>
  <dialog
    ref="dialog"
    closedby="none"
    :aria-labelledby="denied ? 'age-denied' : 'age-title'"
    :aria-describedby="denied ? undefined : 'age-body'"
    class="m-auto w-[min(28rem,calc(100vw-2rem))] rounded-2xl border border-border bg-card p-6 text-card-foreground shadow-[0_0_48px_var(--glow)] backdrop:bg-background/90 backdrop:backdrop-blur-sm sm:p-8"
    @cancel.prevent
    @close="onClose"
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
      <img src="/logo.png" alt="" width="80" height="80" class="mx-auto mb-5 size-20 rounded-xl" />
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
  </dialog>
</template>
