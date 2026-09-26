<script setup lang="ts">
import { useReducedMotion } from 'motion-v'
import { onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import AgeGate from './components/AgeGate.vue'
import ScrollSmoke from './components/ScrollSmoke.vue'
import SmokeIntro from './components/SmokeIntro.vue'
import SocialLinks from './components/SocialLinks.vue'
import { smokeTileUrls } from './smoke'

const route = useRoute()
const year = new Date().getFullYear() // §6 footer.claim
const reduceMotion = useReducedMotion()

// Smoke (docs/PLAN.md §4.3): the intro once per full page load and the scroll smoke — neither under
// reduced motion (checked synchronously, so they are never even mounted). The intro holds until
// AgeGate reports the gate closed.
const motionOk = !window.matchMedia('(prefers-reduced-motion: reduce)').matches
const smoke = ref(motionOk)
const gateClosed = ref(false)

// The divider smoke rims (main.css .divider-smoke) take the shared tiles as background images, in
// both motion modes (under reduced motion they stay as a static texture). Until the class is set
// the rims are hidden. Built after the first paint (rAF, then a task), so the ~20 ms
// of tile work never delays the intro's first frame.
onMounted(async () => {
  await new Promise((r) => requestAnimationFrame(() => setTimeout(r)))
  const [a, b, reach] = await smokeTileUrls()
  const root = document.documentElement
  root.style.setProperty('--smoke-tile-a', `url("${a}")`)
  root.style.setProperty('--smoke-tile-b', `url("${b}")`)
  root.style.setProperty('--smoke-reach', `url("${reach}")`)
  root.classList.add('smoke-rim')
})

// Already on /#standorte: the router sees a duplicate navigation and does not scroll again,
// so scroll here (the section's scroll-mt-20 keeps it clear of the header).
function toLocations(e: MouseEvent) {
  if (route.path !== '/' || route.hash !== '#standorte') return
  e.preventDefault()
  document
    .getElementById('standorte')
    ?.scrollIntoView({ behavior: reduceMotion.value ? 'auto' : 'smooth', block: 'start' })
}
</script>

<template>
  <div class="flex min-h-svh flex-col">
    <a
      href="#main"
      class="sr-only rounded-lg bg-primary font-semibold text-primary-foreground focus:not-sr-only focus:fixed focus:top-3 focus:left-3 focus:z-[1300] focus:px-4 focus:py-3"
      >Zum Inhalt springen</a
    >
    <header
      class="sticky top-0 z-[1200] border-b border-border/60 bg-background/80 backdrop-blur-md"
    >
      <div class="mx-auto flex h-16 max-w-6xl items-center justify-between px-4">
        <RouterLink to="/" class="rounded-lg">
          <img src="/logo.webp" alt="SMOKE" width="40" height="40" class="size-10 rounded-lg" />
        </RouterLink>
        <div class="flex items-center gap-1">
          <RouterLink
            :to="{ path: '/', hash: '#standorte' }"
            class="inline-flex min-h-11 items-center rounded-lg px-3 font-semibold text-muted-foreground transition-colors duration-200 hover:bg-muted hover:text-foreground"
            @click="toLocations"
            >Standorte</RouterLink
          >
          <SocialLinks variant="icon" />
        </div>
      </div>
    </header>

    <main id="main" tabindex="-1" class="flex-1 focus:outline-none">
      <RouterView v-slot="{ Component }">
        <Transition name="fade" mode="out-in">
          <component :is="Component" />
        </Transition>
      </RouterView>
    </main>

    <footer class="border-t border-border/60">
      <div
        class="mx-auto flex max-w-6xl flex-col items-center gap-1 px-4 py-6 text-sm sm:flex-row sm:justify-between"
      >
        <p class="text-center text-muted-foreground text-balance">
          &copy; {{ year }} Smoke MKK -
          <span class="whitespace-nowrap">Vapes, Drinks, Snacks &amp; More!</span>
        </p>
        <nav class="flex flex-wrap items-center justify-center gap-2">
          <RouterLink
            to="/impressum"
            class="rounded-md px-3 py-2.5 text-muted-foreground transition-colors duration-200 hover:text-foreground"
            >Impressum</RouterLink
          >
          <RouterLink
            to="/datenschutz"
            class="rounded-md px-3 py-2.5 text-muted-foreground transition-colors duration-200 hover:text-foreground"
            >Datenschutz</RouterLink
          >
        </nav>
      </div>
    </footer>

    <ScrollSmoke v-if="motionOk" />
    <SmokeIntro v-if="smoke" :hold="!gateClosed" @done="smoke = false" />
    <AgeGate @closed="gateClosed = true" />
  </div>
</template>
