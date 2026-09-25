<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()
const isImprint = computed(() => route.name === 'impressum')
</script>

<template>
  <article class="mx-auto max-w-3xl px-4 py-16">
    <h1 class="text-3xl font-bold uppercase tracking-tight sm:text-4xl">
      {{ isImprint ? 'Impressum' : 'Datenschutzerklärung' }}
    </h1>
    <!--
      TODO (operator): replace with the real legal text.
      Datenschutz must cover (docs/PLAN.md §4.3): OpenStreetMap tile requests (visitor IP goes to
      openstreetmap.org), the localStorage flag "ageConfirmed" set by the 18+ dialog, the outbound
      Instagram/TikTok links, and the optional browser location: asked only when the hero button
      "Automat in deiner Nähe finden" is clicked (reused without asking on later visits only if the
      visitor already granted the browser permission), used only in the browser to mark and sort the
      nearest machines, kept in memory, never stored or sent to a server. (Fonts are self-hosted; no
      third-party font requests.)
    -->
    <p class="mt-6 text-muted-foreground">TODO: Text vom Betreiber einfügen.</p>
  </article>
</template>
