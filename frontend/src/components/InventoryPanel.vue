<script setup lang="ts">
import { AnimatePresence, motion, useReducedMotion } from 'motion-v'
import { computed, onUnmounted, ref, watch } from 'vue'
import { fetchInventory, type Category, type Inventory, type Machine } from '../api'
import BorderBeam from './ui/BorderBeam.vue'

const props = defineProps<{ machine: Machine | null }>()

// docs/PLAN.md §6: verfügbar (qty > 3) · wenige (1–3) · ausverkauft (0)
const LOW_STOCK_MAX = 3

const CATEGORY_LABEL: Record<Category, string> = {
  Vape: 'Vapes',
  Drink: 'Drinks',
  Snack: 'Snacks',
}

const reduceMotion = useReducedMotion()
const state = ref<'idle' | 'loading' | 'error' | 'ready'>('idle')
const inventory = ref<Inventory | null>(null)
// Product ids whose image failed to load; they fall back to the category placeholder.
const brokenImages = ref(new Set<number>())
let controller: AbortController | null = null

watch(
  () => props.machine?.id,
  async (id) => {
    controller?.abort()
    inventory.value = null
    if (id == null) {
      state.value = 'idle'
      return
    }
    state.value = 'loading'
    const c = (controller = new AbortController())
    try {
      inventory.value = await fetchInventory(id, c.signal)
      state.value = 'ready'
    } catch {
      if (!c.signal.aborted) state.value = 'error'
    }
  },
  { immediate: true },
)

onUnmounted(() => controller?.abort())

const groups = computed(() =>
  (Object.keys(CATEGORY_LABEL) as Category[])
    .map((category) => ({
      category,
      label: CATEGORY_LABEL[category],
      items: inventory.value?.items.filter((i) => i.category === category) ?? [],
    }))
    .filter((g) => g.items.length),
)

// §6 panel.summary: n = items with quantity > 0, m = sum of quantities; singular 1 Produkt / 1 Artikel
// ("Artikel" is the same word in singular and plural)
const summary = computed(() => {
  const items = inventory.value?.items ?? []
  return {
    products: items.filter((i) => i.quantity > 0).length,
    units: items.reduce((sum, i) => sum + i.quantity, 0),
  }
})

const updatedAt = computed(() =>
  inventory.value?.updatedAt ? new Date(inventory.value.updatedAt).toLocaleString('de-DE') : null,
)

function stock(quantity: number) {
  if (quantity === 0)
    return { label: 'ausverkauft', text: 'text-destructive-foreground', dot: 'bg-destructive' }
  if (quantity <= LOW_STOCK_MAX) return { label: 'wenige', text: 'text-warning', dot: 'bg-warning' }
  return { label: 'verfügbar', text: 'text-success', dot: 'bg-success' }
}

const price = (cents: number) =>
  (cents / 100).toLocaleString('de-DE', { style: 'currency', currency: 'EUR' })
</script>

<template>
  <div
    class="relative flex h-full flex-col overflow-hidden rounded-xl border border-border bg-card p-5 text-card-foreground"
  >
    <BorderBeam
      v-if="machine"
      :size="140"
      :duration="8"
      :border-width="2"
      color-from="var(--secondary)"
      color-to="var(--accent)"
    />
    <h3 class="text-sm font-semibold uppercase tracking-[0.2em] text-secondary">
      Aktueller Bestand
    </h3>
    <!-- Switching machines cross-fades: arrive decelerating, leave accelerating (ui-ux-pro-max). -->
    <div class="flex min-h-0 flex-1 flex-col" aria-live="polite" :aria-busy="state === 'loading'">
      <AnimatePresence mode="wait">
        <motion.div
          :key="machine?.id ?? 'none'"
          :initial="reduceMotion ? false : { opacity: 0 }"
          :animate="{ opacity: 1, transition: { duration: reduceMotion ? 0 : 0.2, ease: 'easeOut' } }"
          :exit="{ opacity: 0, transition: { duration: reduceMotion ? 0 : 0.15, ease: 'easeIn' } }"
          class="flex min-h-0 flex-1 flex-col"
        >
          <p v-if="machine" class="mt-1 text-xl font-bold">{{ machine.name }}</p>
          <p
            v-if="state === 'ready' && inventory?.items.length"
            class="mt-1 text-sm text-muted-foreground tabular-nums"
          >
            {{ summary.products }} {{ summary.products === 1 ? 'Produkt' : 'Produkte' }} ·
            {{ summary.units }} Artikel im Automaten
          </p>

          <div class="mt-4 min-h-0 flex-1 overflow-y-auto overscroll-contain">
            <p v-if="state === 'idle'" class="text-muted-foreground">
              Wähle einen Automaten auf der Karte oder in der Liste.
            </p>
            <p v-else-if="state === 'error'" class="text-destructive-foreground">
              Der Bestand konnte gerade nicht geladen werden. Versuch es gleich noch einmal.
            </p>
            <ul v-else-if="state === 'loading'" class="space-y-3" aria-hidden="true">
              <li v-for="n in 5" :key="n" class="h-14 animate-pulse rounded-lg bg-muted" />
            </ul>
            <template v-else-if="inventory">
              <p v-if="!inventory.items.length" class="text-muted-foreground">
                Für diesen Automaten ist noch kein Bestand hinterlegt.
              </p>
              <section v-for="group in groups" :key="group.category" class="mb-5 last:mb-0">
                <h4
                  class="mb-2 text-xs font-semibold uppercase tracking-[0.2em] text-muted-foreground"
                >
                  {{ group.label }}
                </h4>
                <ul class="divide-y divide-border/60">
                  <motion.li
                    v-for="(item, index) in group.items"
                    :key="item.productId"
                    :initial="reduceMotion ? false : { opacity: 0, y: 8 }"
                    :animate="{ opacity: 1, y: 0 }"
                    :transition="{ duration: 0.25, delay: reduceMotion ? 0 : index * 0.04 }"
                    class="flex items-center gap-3 py-2.5"
                  >
                    <!-- Fixed square box: the image never shifts the row (ui-ux-pro-max "Content Jumping"). -->
                    <span
                      :class="[
                        'grid size-14 shrink-0 place-items-center overflow-hidden rounded-lg bg-muted p-1',
                        item.quantity === 0 && 'grayscale opacity-60',
                      ]"
                    >
                      <img
                        v-if="item.imageUrl && !brokenImages.has(item.productId)"
                        :src="item.imageUrl"
                        :alt="item.name"
                        loading="lazy"
                        width="48"
                        height="48"
                        class="size-12 object-contain"
                        @error="brokenImages.add(item.productId)"
                      />
                      <svg
                        v-else
                        class="size-8 text-secondary"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        stroke-width="1.6"
                        stroke-linecap="round"
                        stroke-linejoin="round"
                        aria-hidden="true"
                      >
                        <!-- Drink: can -->
                        <template v-if="item.category === 'Drink'">
                          <path d="M8 5h8l1 2v12a2 2 0 0 1-2 2H9a2 2 0 0 1-2-2V7z" />
                          <path d="M7 9h10M7 17h10M10.5 3h3" />
                        </template>
                        <!-- Vape: disposable device -->
                        <template v-else-if="item.category === 'Vape'">
                          <rect x="8" y="7" width="8" height="15" rx="2.5" />
                          <path d="M10 7V4.5a1.5 1.5 0 0 1 1.5-1.5h1A1.5 1.5 0 0 1 14 4.5V7" />
                          <path d="M11 18h2" />
                        </template>
                        <!-- Snack: bag -->
                        <template v-else>
                          <path d="M6 3h12l-1.5 3L18 9v10l1 2H5l1-2V9l1.5-3z" />
                          <path d="M7.5 6h9M9 13.5c1.5-1.5 4.5-1.5 6 0" />
                        </template>
                      </svg>
                    </span>

                    <span class="min-w-0 flex-1">
                      <span class="line-clamp-2 font-medium">{{ item.name }}</span>
                      <span class="mt-0.5 flex flex-wrap items-center gap-x-2 text-sm">
                        <span
                          :class="[
                            'inline-flex items-center gap-1.5 whitespace-nowrap',
                            stock(item.quantity).text,
                          ]"
                        >
                          <span
                            :class="['size-2 rounded-full', stock(item.quantity).dot]"
                            aria-hidden="true"
                          />
                          {{ stock(item.quantity).label }}
                        </span>
                        <span class="whitespace-nowrap text-muted-foreground tabular-nums"
                          >{{ item.quantity }} Stück</span
                        >
                      </span>
                    </span>
                    <span class="shrink-0 font-semibold tabular-nums">{{
                      price(item.priceCents)
                    }}</span>
                  </motion.li>
                </ul>
              </section>
              <p v-if="updatedAt" class="mt-4 text-sm text-muted-foreground">
                Stand: {{ updatedAt }}
              </p>
            </template>
          </div>
        </motion.div>
      </AnimatePresence>
    </div>
  </div>
</template>
