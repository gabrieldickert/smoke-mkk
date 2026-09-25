import { createRouter, createWebHistory } from 'vue-router'
import HomeView from './views/HomeView.vue'
import LegalView from './views/LegalView.vue'

export default createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/', name: 'home', component: HomeView },
    { path: '/impressum', name: 'impressum', component: LegalView },
    { path: '/datenschutz', name: 'datenschutz', component: LegalView },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
  async scrollBehavior(to, _from, savedPosition) {
    // App.vue's <Transition name="fade" mode="out-in"> keeps the old page in the DOM while it fades
    // out. Scrolling before it is gone clamps to the old page's height (back would land at the top)
    // and jumps the page that is fading out, so wait until no .fade-leave-active element is left.
    await new Promise<void>(function check(resolve) {
      requestAnimationFrame(() =>
        document.querySelector('.fade-leave-active') ? check(resolve) : resolve(),
      )
    })
    if (savedPosition) return savedPosition
    if (to.hash) {
      const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches
      // top: 80 = 64 px sticky header + gap; the router ignores CSS scroll-margin.
      return { el: to.hash, top: 80, behavior: reduce ? 'auto' : 'smooth' }
    }
    return { top: 0 }
  },
})
