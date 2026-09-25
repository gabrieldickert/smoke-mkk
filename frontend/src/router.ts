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
  scrollBehavior: () => ({ top: 0 }),
})
