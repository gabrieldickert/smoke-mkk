<script setup lang="ts">
import AgeGate from './components/AgeGate.vue'
import SocialLinks from './components/SocialLinks.vue'
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
          <img src="/logo.png" alt="SMOKE" width="40" height="40" class="size-10 rounded-lg" />
        </RouterLink>
        <SocialLinks variant="icon" />
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
      <nav class="mx-auto flex max-w-6xl flex-wrap items-center justify-center gap-2 px-4 py-6 text-sm">
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
    </footer>

    <AgeGate />
  </div>
</template>
