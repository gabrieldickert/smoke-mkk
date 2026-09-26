# Smoke MKK — Phase 1 implementation plan

*Status: T0, B1–B3, F1–F13 done and committed (F10 = UX audit + owner additions, aurora-everywhere still a trial; F11–F13 = smoke intro, scroll/divider smoke, bigger hero headline, 2026-09-26) · API + site verified end to end without Docker (local Postgres 16 on 5433, see README) · T2 compose run still blocked on WSL · Owner: the PM/orchestrator (`.claude/agents/smokemkk-pm.md`).*

This is the constitution for phase 1. Anyone — human or agent — picking the project up reads `CLAUDE.md` first, then this file, then takes a ticket from §9. The two specialists implement against §5 (contract) and §6 (copy) **as written here**. Changing either is the PM's decision and is written back here before anyone codes against it.

## 1. Context

Smoke MKK operates 12 self-service vending machines (vapes, drinks, snacks) in Hessen, Germany. Their only web presence is a Linktree (https://linktr.ee/smoke.mkk) with one Google-Maps link per machine plus Instagram/TikTok. Goal: an own website that replaces the Linktree — a Vue SPA with an OpenStreetMap store finder where every machine is a marker and clicking it shows that machine's current inventory. A B2C/B2B shop follows later (§11). Everything runs in Docker: Vue.js, ASP.NET Core, PostgreSQL.

**Phase 1 scope:** SPA (animated hero, map + inventory panel, machine cards, 18+ modal, Impressum/Datenschutz), a minimal API, Postgres, docker compose.

## 2. Decisions taken with the owner (2026-09-25)

| Topic | Decision | Consequence |
|---|---|---|
| Inventory source | **Manual**, via an API-key-protected `PUT` (no telemetry exists yet) | placeholder catalogue is seeded; operator replaces it with curl |
| UI language | **German only**, informal *du* (young Instagram audience — deliberately not CareFlow's *Sie*) | no i18n library |
| Age gate | **Simple 18+ modal** on first visit, remembered in `localStorage` | cosmetic; legally sufficient verification comes with the shop |
| Styling | **Tailwind 4 + Inspira UI + motion-v** | see §4.3; no UI kit, no shadcn-vue in phase 1 |
| Design method | **`ui-ux-pro-max` is invoked at the start of every frontend ticket** — design, components, layout, review | structural: frontmatter + first-action rule in `smokemkk-frontend` |
| Engineering skills | **addyosmani/agent-skills** plugin enabled project-wide (`.claude/settings.json`) | `CLAUDE.md` §1 maps work → skill |
| Watermelon UI | https://github.com/WatermelonCorp/watermelon-platform is React 19 / Radix / Motion-for-React — **not embeddable in Vue**. Visual reference only. | a Watermelon block may be hand-ported to Vue with motion-v when a specific one is wanted |

## 3. Research results (seed data)

**Brand:** handle `smoke.mkk`, name "SMOKE", tagline "VAPES | DRINKS | SNACKS". Google lists the machines as "SMOKE Vape Snacks & Getränke Automat".
**Socials:** Instagram https://www.instagram.com/smoke.mkk · TikTok https://www.tiktok.com/@smokemkk
**Logo:** https://ugc.production.linktr.ee/92cc0284-ca28-4922-bd77-869d631f5cd0_sdfsdf.png → download once to `frontend/public/logo.png` (F1).

**Machines.** Coordinates from Nominatim (street addresses) or from the Google-Maps redirect URL (pins), then reverse-geocoded for a display address. Slugs are the seed's stable keys.

| # | slug | name | street | postal_code | city | lat | lng | is_active | note |
|---|---|---|---|---|---|---|---|---|---|
| 1 | fulda | SMOKE Fulda | Niesiger Str. 69 | 36039 | Fulda | 50.56899 | 9.66503 | true | at Nahkauf supermarket |
| 2 | weisskirchen | SMOKE Weisskirchen | Hoher Nickel 14 | 63110 | Rodgau | 50.05644 | 8.88740 | true | |
| 3 | langenselbold | SMOKE Langenselbold | Steinweg 1A | 63505 | Langenselbold | 50.17634 | 9.04093 | true | |
| 4 | schluechtern | SMOKE Schlüchtern | Krämerstraße 14 | 36381 | Schlüchtern | 50.34747 | 9.52716 | true | |
| 5 | bad-orb | SMOKE Bad Orb | Jahnstraße 42 | 63619 | Bad Orb | 50.22044 | 9.35409 | true | Google pin |
| 6 | jossgrund | SMOKE Jossgrund | Burgstraße 15 | 63637 | Jossgrund (Burgjoß) | 50.20378 | 9.48254 | true | Google pin |
| 7 | burgjoss | SMOKE Burgjoss | *(unknown)* | 63637 | Jossgrund | 50.20380 | 9.48049 | **false** | Linktree link resolves to the Langenselbold pin (data error); coordinates = village centre; probably a duplicate of #6 — owner must confirm |
| 8 | waechtersbach | SMOKE Wächtersbach | Poststraße 33 | 63607 | Wächtersbach | 50.25550 | 9.29381 | true | Google pin, low precision |
| 9 | rothenbergen | SMOKE Rothenbergen | Alte Dorfstraße 2A | 63584 | Gründau | 50.19879 | 9.10918 | true | |
| 10 | hesseldorf | SMOKE Hesseldorf | Brachttalstraße 18 | 63607 | Wächtersbach | 50.27203 | 9.30658 | true | Google pin |
| 11 | hellstein | SMOKE Hellstein | Sandwerkstraße 2 | 63636 | Brachttal | 50.32034 | 9.30018 | true | Google pin |
| 12 | lauterbach | SMOKE Lauterbach | Marktplatz | 36341 | Lauterbach | 50.63632 | 9.39614 | true | OSM already has a "Smoke" POI here |

Google-Maps URLs for the `google_maps_url` column. #1–#4 use `https://www.google.com/maps/search/?api=1&query=<url-encoded address>`. #5–#12 use the original Linktree short links:

| # | google_maps_url |
|---|---|
| 5 | https://goo.gl/maps/xXyGLJiPCcnzB36h9 |
| 6 | https://goo.gl/maps/xX4skQTj9qrPxpgu8 |
| 7 | `null` — its Linktree link (https://maps.app.goo.gl/dHhCqpEpu9PtY9Xf7) points to Langenselbold, which is wrong |
| 8 | https://goo.gl/maps/r33J2pLgynRgCQ8K8 |
| 9 | https://maps.app.goo.gl/4w2qC9V7qHrYzuxk9 |
| 10 | https://goo.gl/maps/AxoLgpYdEnATwhj67 |
| 11 | https://maps.app.goo.gl/z3jF2pLDndePZfL87 |
| 12 | https://maps.app.goo.gl/9j1BeUM7qcCZ44b67 |

**Placeholder catalogue** (real catalogue not public — owner confirms or replaces it; mark in README as `TODO`). Named like real vending products so the product images make sense (owner request, 2026-09-25):

| id | name | category | image_url |
|---|---|---|---|
| 1 | Elf Bar 600 Blueberry Ice | Vape | `/products/elfbar-600-blueberry-ice.webp` |
| 2 | Elf Bar 600 Watermelon | Vape | `/products/elfbar-600-watermelon.webp` |
| 3 | Elf Bar 600 Cola | Vape | `/products/elfbar-600-cola.webp` |
| 4 | Red Bull Energy Drink 250 ml | Drink | `/products/red-bull-250.webp` |
| 5 | Coca-Cola Zero 330 ml | Drink | `/products/coca-cola-zero-330.webp` |
| 6 | Vio Wasser still 500 ml | Drink | `/products/vio-still-500.webp` |
| 7 | Snickers | Snack | `/products/snickers.webp` |
| 8 | Haribo Goldbären 100 g | Snack | `/products/haribo-goldbaeren-100.webp` |
| 9 | Pringles Paprika 40 g | Snack | `/products/pringles-paprika-40.webp` |
| 10 | Marlboro Red 20 Stück | Tobacco | `/products/marlboro-red-20.webp` |
| 11 | Pueblo Classic Tabak 30 g | Tobacco | `/products/pueblo-classic-30.webp` |
| 12 | OCB Slim Premium Papers | Accessory | `/products/ocb-slim-premium.webp` |
| 13 | Clipper Feuerzeug | Accessory | `/products/clipper-feuerzeug.webp` |

**Categories (owner, 2026-09-25):** Smoke MKK sells five: Vapes, Tabak, Rauchzubehör, Drinks, Snacks. Wire values `Vape | Tobacco | Accessory | Drink | Snack`, in that order, which is also the display and sort order (age-restricted goods first). Prices for the new seed rows: tobacco 950–1100, accessories 150–350 cents. VELO nicotine pouches were dropped from the placeholder: nicotine pouches are not legally sold in Germany.

**Placeholder product illustrations (owner decision, 2026-09-25).** No freely licensed, transparent, matching photos exist for these branded products (Wikimedia Commons has none; Open Food Facts photos are user snapshots with backgrounds and mixed angles). So until real packshots arrive, the frontend ships **13 brand-free illustrations**, one per §3 product, at exactly the §3 file names:
- Format: 400×400 **WebP with alpha**, fully transparent background, no drop shadow baked in (the panel adds one in CSS if wanted).
- Uniform by construction: one script draws all 13 from shared shapes (can, bottle, disposable vape, cigarette pack, tobacco pouch, rolling-paper booklet, lighter, bar wrapper, gummy bag, chip tube). Same canvas, same product height band (bottom edge at y≈360, tallest item ≈320 px), same light from top-left, same outline weight, same corner radius.
- Brand-free: generic German labels only (e.g. `ENERGY`, `COLA ZERO`, `WASSER`, `KAUGUMMI`-style words, flavour names), colours that hint at the product. **No logos, no trademarked marks, no brand names or lettering styles.** This keeps both copyright and trademark out of it.
- Palette: product colours may be bright, but the silhouettes must read on the dark card background (`--card`); check at 56 px.
- Sources in the repo: `frontend/scripts/product-art/` holds the one script and a README explaining how to re-run it. Its outputs go to `frontend/public/products/`. The script uses Pillow (already installed on the dev machine), a dev-only tool that is not an app dependency and is not needed by the Docker build.
- Real photos later replace files one by one under the same names; no code change. The category SVG fallback stays for missing files.

**Product images.** `image_url` is a site-relative path; the file lives in `frontend/public/products/`. Brand packshots are copyrighted, so **nobody downloads them from the web**. The owner supplies real photos (own photos of the machine stock, or packshots from their wholesaler with usage rights), saved under exactly these file names as square WebP, about 400×400. Until a file exists, the frontend shows a category placeholder (§4.3), so the site never shows a broken image.

Seed stock: every active machine gets all 13 products; quantities rotate per machine through `7, 5, 0, 12, 2, 8, 6, 0, 3, 9, 1, 4, 10` so all three display states (§6) appear on every machine; `price_cents` plausible (vapes 899–1299, tobacco 950–1100, accessories 150–350, drinks 250–350, snacks 150–250); `updated_at` = seed time.

## 4. Architecture

Versions (confirmed Sept 2026): Vue 3 + Vite 8, Tailwind 4, Leaflet **1.9.4** (2.0 is alpha — stay on 1.9), motion-v, .NET 10, `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.x, `postgres:17-alpine`, Node 24.

### 4.1 Layout and ownership

```
C:\smokemkk\
  CLAUDE.md  docs/  README.md  .gitignore  .env.example   ← PM
  docker-compose.yml  .claude/                            ← PM
  backend/                                                ← smokemkk-backend
    Dockerfile                sdk:10.0 build → aspnet:10.0 (listens on 8080, non-root)
    .dockerignore             bin, obj
    SmokeMkk.Api.csproj
    Properties/launchSettings.json   applicationUrl http://localhost:5000
    Program.cs                DI, Migrate() + seed, 4 endpoints, DTO records (~120 lines)
    Db.cs                     DbContext + Machine, Product, MachineInventory
    Seed.cs                   idempotent seed of §3
    Migrations/               generated by dotnet-ef
  frontend/                                               ← smokemkk-frontend
    Dockerfile                node:24-alpine build → nginx:alpine
    .dockerignore             node_modules, dist
    nginx.conf                SPA fallback + proxy /api → api:8080
    vite.config.ts            vue() + tailwindcss(); dev proxy /api → http://localhost:5000
    index.html                <html lang="de" class="dark">, title/description/OG tags
    public/                   logo.png, favicon.svg, robots.txt
    src/
      main.ts                 app + router; imports assets/main.css and leaflet/dist/leaflet.css
      assets/main.css         @import "tailwindcss"; @import "tw-animate-css"; Inspira token block; component @theme entries
      router.ts               "/", "/impressum", "/datenschutz"
      api.ts                  types (§5, mirrored verbatim) + two fetch functions
      App.vue                 header (logo, Instagram/TikTok), <RouterView>, footer (Impressum/Datenschutz), <AgeGate>
      components/ui/          copied Inspira components, unchanged (see 4.3)
      components/AgeGate.vue
      components/MachineMap.vue
      components/InventoryPanel.vue    machine list + selected machine's stock (F10)
      views/HomeView.vue
      views/LegalView.vue
```

### 4.2 Backend — ASP.NET Core minimal API + EF Core + Npgsql

Entities (EF defaults, no naming plugin):

- `Machine(Id, Slug, Name, Street, PostalCode, City, Lat, Lng, IsActive, GoogleMapsUrl)` — `Slug` unique
- `Product(Id, Name, Category, ImageUrl)` — `ImageUrl` is a nullable site-relative path (§3); `Category` is a C# enum `Vape | Tobacco | Accessory | Drink | Snack` (this declaration order is the sort order) stored as string (`HasConversion<string>()`), so a later age filter is a WHERE clause
- `MachineInventory(MachineId, ProductId, Quantity, PriceCents, UpdatedAt)` — composite PK; `Quantity >= 0` and `PriceCents >= 0` as check constraints

Rules:
- JSON: camelCase (default) + `JsonStringEnumConverter` globally via `ConfigureHttpJsonOptions`.
- `GET /api/machines` output-cached 60 s (`AddOutputCache`). Inventory endpoints are **not** cached, so a `PUT` is visible immediately.
- API key: header `X-Api-Key` compared with `CryptographicOperations.FixedTimeEquals` against env `ADMIN_API_KEY`; missing env → the PUT returns 503 (never "open"). Never log the key.
- Startup: `db.Database.Migrate()` (single replica → no separate migration container), then `Seed.Run(db)` = insert §3 only if `Machines` is empty, so operator edits are never overwritten. **Not** `HasData`.
- `UseNpgsql(cs, o => o.EnableRetryOnFailure())` covers the first-boot window where `pg_isready` passes before the DB is fully created.
- No repositories, services layer, MediatR, AutoMapper, or test project. DTOs are `record`s next to the endpoints in `Program.cs`.

### 4.3 Frontend — Vue 3 + Vite + TS + Tailwind 4 + Inspira UI + motion-v

**Every frontend ticket starts by invoking the `ui-ux-pro-max` skill** (design system, palette, typography, layout, UX guidelines) — not only F1. In F1 it additionally decides palette + font pairing; later tickets use it to review what they touch.

Scaffold: `npm create vue@latest frontend` → TypeScript + Router only (no Pinia, no test runners).
Dependencies: `tailwindcss @tailwindcss/vite` (dev), `@vueuse/core motion-v tw-animate-css @inspira-ui/plugins leaflet`, `@types/leaflet` (dev). Nothing else without a PM decision written here. (No `clsx`/`tailwind-merge`/`lib/utils.ts`: the Inspira components import `cn` from `@inspira-ui/plugins`.)

Theme: paste Inspira's "Other TailwindCSS Kit" token block from its installation guide (`content/en/1.getting-started/2.installation.md` in the unovue/inspira-ui repo) into `main.css`, keep `class="dark"` on `<html>` permanently (one dark brand theme; the light values stay but are never shown). Write the `ui-ux-pro-max` palette + fonts into the token block (`--primary`, `--accent`, `--background`, `--card`, `--border` …) — colours live nowhere else. Check contrast ≥ 4.5:1 for body text.

Inspira components — copy the `.vue` file verbatim into `src/components/ui/<Name>.vue` from
`https://raw.githubusercontent.com/unovue/inspira-ui/main/app/components/inspira/ui/<kebab>/<Pascal>.vue`,
and apply the `#instructions` block of the matching doc `content/en/2.components/<category>/<kebab>.md` (some need an `@theme inline` keyframes entry in `main.css`):

| Component | Use | Doc category |
|---|---|---|
| `AuroraBackground` | hero backdrop (CSS-only) | backgrounds |
| `FlipWords` | hero headline cycling the five categories (§6 `hero.words`) | text-animations |

`CardSpotlight` (machine cards) and `BorderBeam` (panel accent) were used until F10 and deleted there: the card grid moved into the panel, and an endless decorative loop next to a list being read breaks the `ui-ux-pro-max` "continuous animation" rule.

Own components:
- `MachineMap.vue` — raw Leaflet in `onMounted` on a `ref` div, destroyed in `onUnmounted`. Tiles `https://tile.openstreetmap.org/{z}/{x}/{y}.png` with attribution `&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>-Mitwirkende` (mandatory; swap to CARTO Voyager is a one-line URL change if traffic grows). Markers via `L.divIcon` with a Tailwind-styled class (avoids Leaflet's broken default-icon paths under Vite). `fitBounds` over all machines with padding. Props `machines`, `selectedId`; emits `select(id)`. Selected marker gets a distinct class.
- **UX audit changes (F10, 2026-09-25), these override the older bullets below where they differ:**
  - *Panel = list + detail.* `InventoryPanel` gets `machines`, `status` (`loading | ready | error`) and the selected `machine`, and emits `select(id | null)`. No machine selected: `panel.empty`, then one button per machine (display name + address), skeleton rows while loading, `map.loadError` (`role="alert"`) on error — the map itself gets no overlay. Machine selected: a `panel.back` button, the display name as the panel's `<h3>` (`panel.title` is the list view's `<h3>` and is not shown in the detail view), the address, the `panel.route` link when `googleMapsUrl` is set, then `panel.summary` with `panel.updatedAt` directly under it (`toLocaleString("de-DE", { dateStyle: "medium", timeStyle: "short" })`, no seconds), then the groups; within a category, items in stock come first (stable, so name order stays). This list is the text fallback for the map; `MachineCard.vue` and the card grid are deleted.
  - *Search (owner request, 2026-09-25):* above the machine list, one native `<input type="search">` with a visible `<label>` (`search.label`) and `search.placeholder`, ≥ 44 px tall. It filters the list as you type: a machine matches when the query is a prefix of its `postalCode` or appears in its display name, `city` or `street`; case- and accent-insensitive (`normalize("NFD")`, strip diacritics, lower-case, trimmed), so `schluchtern` finds Schlüchtern. Empty query → all machines. No match → `search.noResults` instead of the list. The persistent status line announces `search.count` while a query is set. The query survives a detail view and `panel.back`. The map markers are not filtered (the map stays the overview). Plain `computed` filter, no library.
  - *Screen readers:* no `aria-live` around the list. One persistent visually hidden `role="status"` line announces `{display name} · {panel.summary}`, `panel.noStock` or `panel.error`.
  - *Display name:* the machine name without the leading `SMOKE ` (one helper next to the fetch functions in `api.ts`); the header carries the brand.
  - *Map:* each marker gets `bindTooltip(display name)` in the token colours (Leaflet 1.9 opens it on hover and keyboard focus) and no `title`. Selecting a machine sets the view to `max(current zoom, 12)` on it (no animation under reduced motion), so neighbours that overlap at the start zoom separate; deselecting fits all machines again. On coarse pointers (`matchMedia("(pointer: coarse)")`) one-finger dragging starts disabled and turns on after the first tap on the map, so the page scrolls past the map; pinch zoom stays. Zoom buttons are 44 px on touch.
  - *Phones:* below `md`, every selection (marker, list, CTA) scrolls the panel into view, clear of the sticky header.
  - *Hero CTA:* keeps its scroll + focus, then asks `navigator.geolocation` once and selects the nearest machine (flat-earth distance is enough at this scale). Denied, unavailable or timed out → nothing more happens; the map is already on screen. The position never leaves the browser.
  - *Nearest machine marking (owner request, 2026-09-25):* whenever a position is known, (1) the map shows the visitor's position as a small distinct dot (`--foreground` core, `--primary` ring, not interactive, tooltip/`aria-label` `geo.you`); (2) the nearest machine's marker gets a persistent extra ring and its tooltip reads `{display name} · geo.nearest`; (3) the panel list is sorted by distance, each row shows `geo.distance`, and the nearest row carries a `geo.nearest` badge; (4) the detail view of the nearest machine shows the same badge. A position comes from the hero CTA (prompt on click, then the nearest machine is also selected as above) or, without any prompt, on page load when `navigator.permissions.query({ name: "geolocation" })` already reports `granted` (then only mark and sort, never select or scroll). Never prompt on page load. Position lives in memory only (no storage).
  - *First screen = hero + pitch (owner request, 2026-09-25):* hero and pitch band together fill exactly the first screen: one wrapper `min-h-[calc(100svh-4rem)]` as a flex column, split 60 / 40 (owner, 2026-09-25; was 70 / 30): hero `flex: 6 1 0`, pitch band `flex: 4 1 0`, both with content centred (divider line included in the pitch share), so both are fully visible before any scroll and the map section starts right below the fold (the owner's choice over the audit's "map sooner"). Both must fit their share at 1280×800 and 375×812 (pitch ≈ 295 px there) — tighten paddings, logo and type sizes (pitch title/body smaller on phones) rather than overflow; the 60/40 ratio may drift by a few percent where content cannot shrink further, never by clipping; on shorter phones the wrapper grows instead of clipping. At the bottom centre of this first screen sits a down-caret link (`href="#standorte"`, inline chevron SVG `aria-hidden`, `aria-label` = `section.locations`, ≥ 44 px, visible focus) that does the hero CTA's scroll + focus, without the location prompt. It may bounce gently three times after load, then rests; no motion under reduced motion. No endless loop. It is only there while the map section has not been reached (owner request, 2026-09-25): clicking it fades it out at once, and it is hidden whenever `#standorte`'s top is inside the viewport or above it (one native `IntersectionObserver` on the section, or the section's `getBoundingClientRect` on scroll); scrolling back to the first screen fades it in again. Fade = opacity ~300 ms; the hidden state also sets `visibility: hidden` after the fade (transition `visibility 0s 300ms`) so an invisible caret is neither clickable nor in the tab order. Instant under reduced motion.
  - *Glowing dividers (owner request, 2026-09-25):* both rules — hero ↔ pitch band and pitch band ↔ `#standorte` — use one shared divider class; each is a 2 px line with a violet → rose → violet gradient (`--primary`, `--accent`), a soft glow (`box-shadow` / blurred copy in `--glow`), fading out towards both edges, pulsing slowly: glow opacity ~0.5 → 1 → 0.5 over ~3 s, ease-in-out, animating opacity only (no layout, no box-shadow keyframes). A deliberate owner exception to the `ui-ux-pro-max` "no endless decorative animation" rule, kept slow and small; under reduced motion it is static at full glow. CSS keyframes in `main.css`, no dependency.
  - *Aurora on all sections — TRIAL, pending the owner's verdict (2026-09-25):* the hero's aurora backdrop runs behind the whole home page: one aurora layer, `position: fixed` behind the content (not one copy per section — each is a blurred, blended, animated full-size layer), sections without their own opaque background so it shows through; map and panel keep their card/map surfaces so text contrast is unchanged. Built as one clearly revertible change (a single wrapper/class in `HomeView.vue` + `main.css`). If the owner rejects it, the aurora goes back to the hero only.
  - *Heroic tagline (owner request, 2026-09-25):* `hero.tagline` is no longer a small body line: display type (Space Grotesk bold, uppercase, tight tracking like the headings), clearly the second-strongest element after the FlipWords headline — roughly `text-2xl` on phones up to `text-4xl`/`text-5xl` on desktop — with the count `{n}x` as the accent (larger, `text-secondary` with the `--glow` text-shadow the headline uses) and the region in `--foreground`. Same words, same line-break rules (`Main-Kinzig-Kreis` and `und Umgebung` never split), still hidden but height-reserving until the count is known. It must keep fitting the hero's 60 % share at 375×812.
  - *Hero actions at the bottom (owner request, 2026-09-25):* inside the hero's 60 % share the content splits into two groups: three rows in a flex column: the brand group (logo, FlipWords headline, tagline) at the top; the social block (`hero.social.lead`, Instagram/TikTok) anchored to the bottom with a comfortable gap (≈ 1.5–2rem) above the hero ↔ pitch divider; and the `hero.cta.map` button in a `flex-1` row between them, vertically centred, so its gaps to the tagline and to the social block are equal (owner, 2026-09-25). On short screens the rows keep a minimum gap (≈ 1.5rem) instead of overlapping. Focus order unchanged (CTA before the social links).
  - *Headline down to the CTA, bigger (owner request, 2026-09-26; overrides the row split above):* only the logo stays at the top. The FlipWords headline and the tagline move down and form one group with the `hero.cta.map` button: headline → tagline → button with tight, fixed gaps (≈ 1–1.5rem to the button), and this group sits centred in the `flex-1` middle row. The free space now lies between the logo and the headline, and between the button and the social block. The headline gets clearly bigger: on desktop the cap rises from 3.5rem to ≈ 4.5–5rem; on phones it grows as far as `RAUCHZUBEHÖR` still fits one line from 320 px up (today it uses 292 of 343 px at 375 px, so ≈ 10–15 % headroom). The tagline keeps its size, still second after the headline. It must still fit the hero's 60 % share at 1280×800 and 375×812; take room from the gaps, never by clipping, and do not change the 60/40 ratio. If 5rem does not fit at 1280×800, use the largest size that does. No layout jump while the words swap (`min-h` reserve as before). Focus order unchanged. **F13 outcome (2026-09-26):** 5rem gave 64/36 at 1280×800, so the cap is **4.5rem** (72 px); fluid `(100vw − 3rem)/7.9` below → 34 px at 320, 41 px at 375; group gaps tightened to 1rem (headline→tagline `mt-2`, tagline→button `mt-4`, row min padding `py-4`); measured 60.1/39.9 at 1280×800 and 60.0/40.0 at 375×812.
  - *Hero:* content height (no full-screen `min-h`), logo 96 px / 128 px from `sm`. Page order stays hero → pitch band → `#standorte` (map + panel) → footer (owner, 2026-09-25: the map is not the middle section).
  - *Logo file:* `public/logo.webp`, 256×256, for every `<img>` on the page; `logo.png` stays for `og:image` and `apple-touch-icon`.
  - *Header:* a `header.locations` link to `/#standorte` next to the social icons, on every page.
  - *Age gate:* stays closed on `/impressum` and `/datenschutz` (decided after `router.isReady()`, so it never flashes there) and opens when an unconfirmed visitor leaves them; the dialog carries the two footer links in both states. Legal pages must stay reachable (`CLAUDE.md` §5.5).
  - *Motion:* section reveals play once (`once: true` everywhere, no fade-out on leaving).
- **F10 outcomes accepted by the PM (2026-09-25):** hero logo 96 px at all sizes and FlipWords headline capped at 3.5rem (not 128 px / 4.5rem), so the hero fits its 60 % share at 1280×800; the tagline count stays lower-case `11x`; the visitor dot is hoverable (tooltip) but neither clickable nor focusable; `Reveal` lost its unused `delay` prop; `AuroraBackground` lost upstream's redundant outer `<div>` so the classed element is the flex item of the 60/40 split; the divider pulse is the owner's exception to the "continuous animation" rule. The aurora-on-all-sections trial is shipped behind one class (`aurora-everywhere` on `HomeView`'s root + the `TRIAL` block in `main.css`) until the owner decides.
- `InventoryPanel.vue` — prop `machine | null`; loads inventory on change; groups by category. Each item is a row or tile with the **product image** (`<img :src="imageUrl" :alt="name" loading="lazy" width height>` in a fixed square box, `object-contain`), name, price, the **quantity as a number** (`panel.quantity`) and the stock state badge per §6 thresholds (one const). The panel header shows `panel.summary` for the selected machine. Image fallback: when `imageUrl` is null or the `<img>` fires `error`, show a category placeholder instead — one inline SVG per category (vape, tobacco pouch or cigarette pack, lighter, can, snack bag) in the brand colours, no image library. Sold-out items stay listed, their image desaturated (`grayscale opacity-60`); price `(priceCents/100).toLocaleString("de-DE", {style:"currency", currency:"EUR"})`; "Stand" from `updatedAt` via `toLocaleString("de-DE")`. Items fade in with `<motion.li>` from motion-v (`initial/animate`), guarded by `useReducedMotion` from motion-v — no animation when the OS asks for reduced motion.
- ~~`MachineCard.vue`~~ — deleted in F10; name, address and the "Route" link (`target="_blank" rel="noopener"`) live in the panel.
- `AgeGate.vue` — native `<dialog>`; `showModal()` in `onMounted` unless `localStorage.getItem("ageConfirmed") === "1"` (wrap storage access in try/catch). "Ja" → set flag, `close()`. "Nein" → swap content to the denial text, keep open. `Escape` disabled (`@cancel.prevent`). Tailwind only.
- `HomeView.vue` — `AuroraBackground` hero (logo, `FlipWords`, tagline, Instagram/TikTok CTAs) → section "Standorte": map left / `InventoryPanel` right (stacked below `md`) (the panel's machine list is the accessibility/SEO text fallback for the map; the F9 pitch band sits between hero and map). Empty/error states per §6.
- `LegalView.vue` — one component, `route.name` decides Impressum vs. Datenschutz; placeholder texts marked `TODO`; Datenschutz must mention OSM tile requests (IP to openstreetmap.org), the `localStorage` age flag, the Instagram/TikTok links, and the optional browser location behind the hero CTA (used only in the browser to find the nearest machine, never sent).
- SEO basics only: `lang="de"`, `<title>`, `meta description`, OG tags, `robots.txt` (allow all). No pre-rendering.

**F1 outcomes accepted by the PM (2026-09-25).**
- Brand look from `ui-ux-pro-max`: dark OLED base `#0F0F23`, neon violet `#7C3AED` + rose `#F43F5E`, card `#1E1C35`, Space Grotesk uppercase display type. Rose is a fill colour only (4.49:1 as text is below the floor).
- Inspira edits allowed and kept: `AuroraBackground` root is a `<div>` (no nested `<main>`); `FlipWords` has `?? ""` guards for `noUncheckedIndexedAccess`; aurora colours and its Tailwind-4 selector bug are handled in `main.css`; `BorderBeam` got an explicit `duration` (deleted in F10).
- `SocialLinks.vue` (shared Instagram/TikTok SVGs) and the dev-only `vite-plugin-vue-devtools` from the scaffold are accepted.
- **Fonts are self-hosted** (`public/fonts/*.woff2` + `@font-face`), never loaded from fonts.googleapis.com: German courts have held that the Google Fonts CDN leaks visitor IPs without consent (LG München I, 2022). Space Grotesk is under the SIL Open Font License, so shipping the files is allowed.
- Dev port is **5174**: 5173 is taken by another project on the dev machine.

**Dark map in the brand palette (owner request, 2026-09-25).** The F1 filter (`invert + hue-rotate`) leaves green forests and red roads, which clash with the violet/rose palette. Keep the OSM tiles (no new third party receiving visitor IPs, no Datenschutz change) and recolour them in CSS only:
- Tile pane: `filter: grayscale(1) invert(1) brightness(0.55) contrast(1.25)`, which gives a neutral near-black map with light-grey roads and labels.
- Brand tint: a pseudo-element or overlay over the tile pane only (not over markers, popups or controls), background `--background`/violet at low alpha with `mix-blend-mode: color` or `multiply`, so land reads as `#0F0F23`-ish and water/roads pick up a faint violet. `pointer-events: none`.
- The map container background uses `--background`, so tiles that are still loading don't flash white or grey.
- Leaflet zoom controls and the attribution box are themed from the tokens (card background, border, text). The attribution stays readable (≥ 4.5:1).
- Markers keep their neon look and must still stand out clearly against the darker base.
- If the CSS result is still not convincing, the fallback is CARTO Dark Matter tiles. That is a new third-party host, so it needs a PM decision first (Datenschutz text, CARTO's free-usage limits for commercial sites, attribution `© OpenStreetMap-Mitwirkende © CARTO`).

**Marker drop-in (owner request, 2026-09-25).** When the map first scrolls into view, the markers are "placed" one after another, like pins dropped onto the map:
- Trigger: a native `IntersectionObserver` on the map container (threshold ≈ 0.3), **once** per page load. Markers stay hidden until the map is visible. If machines load after the map is already visible, they drop in as soon as they arrive.
- Animate an **inner element** of the `L.divIcon` HTML, never the icon element itself: Leaflet positions markers with `transform` on that element, and animating it would break placement on pan and zoom.
- Motion: from `translateY(-28px) scale(0.6)`, opacity 0, to its place with a small overshoot bounce (ease-out-back), ≈ 450 ms, then a brief glow ring on landing. Stagger 50 ms per marker in west→east order (sort by `lng`), so 11 markers land in 950 ms, under 1 s (F4: 60 ms would take 1050 ms).
- CSS keyframes only, no new dependency. The selected-marker style and hover still work after landing.
- Reduced motion: markers appear immediately, no drop and no ring.

**Section transitions (owner request, 2026-09-25).** Sections fade in as they scroll into view and fade out as they leave. Built with motion-v only, no new dependency:
- One wrapper component `components/Reveal.vue`: `<motion.div :initial="{ opacity: 0, y: 24 }" :while-in-view="{ opacity: 1, y: 0 }" :in-view-options="{ once: true, amount: 0.2 }" :transition="{ duration: 0.5, ease: 'easeOut' }">`. (Until F10 it was `once: false`, fading sections out on leaving; re-animating on every scroll pass slowed scanning, so the fade-out was dropped.)
- Wrap each home section with it: hero content, the "Standorte" heading, the map + panel block, and the pitch band.
- Inventory panel: switching machines cross-fades the list (`AnimatePresence` from motion-v, `mode="wait"`, keyed by machine id), in addition to the per-item fade-in.
- Route change `/` ↔ `/impressum` ↔ `/datenschutz`: Vue's built-in `<Transition name="fade" mode="out-in">` around `<RouterView>` in `App.vue`, CSS opacity 200 ms.
- The map itself never fades out while in use: the map block uses `once: true` so Leaflet is not hidden mid-interaction.
- **Reduced motion:** `useReducedMotion()` true → `Reveal` renders a plain `<div>` with no animation, and the route fade is disabled via `@media (prefers-reduced-motion: reduce)`. Content must be fully visible with JavaScript animations off.

**Smoke intro (owner request, 2026-09-26).** The brand is "Smoke": on page load the site sits behind a cloud of smoke that dissolves and reveals it. One own component `components/SmokeIntro.vue`, mounted once in `App.vue`:
- **When:** every full page load (direct load, reload), on every route. SPA navigation never replays it. No storage flag (no Datenschutz change).
- **Age gate coupling:** the smoke holds, thick and gently swirling, while the 18+ dialog is open, so the smoke *is* the gate's backdrop (the dialog's `backdrop:` gets lighter while the smoke is up, so the smoke reads through it; the dialog card itself keeps its contrast). It dissolves as soon as the dialog is closed: at once for confirmed visitors and on `/impressum`, `/datenschutz`; on "Ja" for first-time visitors; on "Nein" it stays. `AgeGate.vue` signals "gate closed" to `App.vue` (one emit or one exposed ref, no store). If the gate later reopens (leaving a legal page unconfirmed), the gate's normal backdrop covers it — no second smoke.
- **Look:** a full-viewport layer above the page (below the dialog's top layer): opaque `--background` base fading out while soft, wispy smoke puffs in a neutral light grey with a faint `--primary` violet tint drift outward from the centre, grow and thin out, like a cloud blown apart. Real smoke texture (turbulent, wispy edges), not flat circles. Dissolve ≈ 1.2–1.6 s, ease-out; the page underneath is fully legible by ~1 s.
- **Technique:** no new dependency. Default: one `<canvas>` with ~30–40 puff sprites pre-rendered once to offscreen canvases (noise-textured radial falloff), animated in one `requestAnimationFrame` loop at device-pixel-ratio ≤ 2; the loop stops and the component unmounts its layer when done (no idle rAF, no leftover DOM). A CSS-only transform/opacity variant is acceptable if it looks as good. Never animate `filter: blur` or SVG turbulence per frame (too slow on phones).
- **Never blocking:** `pointer-events: none`, `aria-hidden="true"` throughout; content underneath is rendered and focusable from the start (the skip link and the dialog work over it). It must not delay the hero's own load animations beyond the dissolve.
- **Reduced motion:** not rendered at all. No JavaScript: nothing covers the page.
- **F11 outcomes accepted by the PM (2026-09-26):** canvas at 1 device pixel (256 px sprites are the detail ceiling, `// ponytail:` comment); the dialog backdrop drops its blur while the smoke is up; the swirl keeps running for a denied visitor (spec, owner's exception to the "continuous animation" rule).

**Scroll smoke and smoking dividers (owner request, 2026-09-26).** Two small follow-ups to the intro, sharing its smoke texture — the sprite generation moves out of `SmokeIntro.vue` into one shared module (e.g. `src/smoke.ts`, built once, lazily), no second texture generator:
- **Scroll smoke:** scrolling puffs out a few small, faint wisps that drift and fade in ~0.8–1.2 s. Spawned by scroll *distance* (e.g. one puff per ~120 px scrolled, capped at ~12 alive), from the left and right viewport edges, drifting against the scroll direction, so they never sit on top of the centred text column. Low alpha (peak ≲ 0.3) so text contrast never drops. Fixed layer, `pointer-events: none`, `aria-hidden`, below the header and the dialog. Idle = no work: no rAF and no timers while nothing is alive (passive scroll listener only). Canvas or DOM sprites with CSS keyframes (transform/opacity), whichever is simpler.
- **Smoking dividers (third version, owner 2026-09-26: "just a subtle border around the divider line, like 5–10 px, where the smoke goes from inwards to outwards"; the rising fumes and the wide 24–40 px drifting band were both rejected):** each glowing divider gets a thin smoke rim hugging the line along its full length, reaching **~16 px on average** above and below it (owner asked for 5–10 px, then 2026-09-26: "the dissolving spread range can be increased a bit"). **The reach is random** (owner, 2026-09-26: "the dissolving range can also be random"): along the line the rim's outer edge is ragged, varying between ≈ 8 and ≈ 26 px, above and below independently, and the pattern shifts slowly over time so no straight edge ever shows. Seeded per load. One way to do it: mask each strip with the vertical fade *combined* (`mask-composite: intersect`) with a horizontally repeated, slowly drifting mask made from the shared noise tile (square, never stretched), so the fade-out distance follows the noise; animating a strip's `mask-position` is acceptable here (thin strips, cheap). The smoke flows *outward* from the line — the half above away upwards, the half below away downwards — **but never straight up or down** (owner, 2026-09-26: "there should be some variance in the direction"): each layer drifts at its own random slant, roughly 15–40° off vertical, leaning left or right at random (the two layers of a strip lean differently, so the smoke crosses and billows), and the direction sways slowly over time (a gentle, eased side-to-side wobble on top of the drift). Seamless loops still hold: per cycle each layer moves a whole number of tiles on both axes (e.g. 1 tile across per 1–3 tiles out ≈ 18–45°), the sway is a separate eased alternate on a wrapper. The smoke is densest right at the line and fading to nothing at the rim's outer edge — **softly blended, no visible hard cut anywhere** (owner, 2026-09-26: the top and bottom rims showed hard cuts): the outward fade is an eased curve (smoothstep-like, several stops), not linear, and reaches zero well inside the clip (≈ 80–85 % of the strip height) so no clip edge can ever show; the noise used for the random reach is soft and low-contrast (blur it once when the tile is generated, never per frame), so the ragged edge is made of soft wisps, not sharp shapes; where top and bottom strips meet at the line there is no seam either, so the line looks like it is steadily giving off smoke into a narrow seam. Fades towards both horizontal ends like the line. Subtle: moderate alpha, tinted with the divider's violet/rose glow; the line stays clearly visible. No sideways band, no wide haze, no puffs. **Never distort the texture** (owner: the v2 band "looks a bit sheared" — caused by `background-size: <random width> 100%` plus a `scaleY` churn): the texture keeps its 1:1 aspect ratio everywhere (square `background-size`, the same px value on both axes), no `scaleX`/`scaleY`/skew on any smoke layer, only `translate` and uniform `scale`; this applies to scroll wisps and intro puffs too. CSS only: two clipped strips (above/below) with the shared tileable texture as `background-image`, an oversized inner layer animated via `transform: translateY` (up for the top strip, down for the bottom), `mask-image` gradients for the vertical and horizontal fades; no layout shift, no horizontal overflow. A further owner exception to the "no endless decorative animation" rule, like the pulse.
- **Randomness (owner, 2026-09-26: "a bit random noise to the smoke"):** no smoke may look mechanical or repeat identically. The noise texture takes a fresh random seed per page load. Every intro and scroll puff gets random jitter in size, rotation, speed, drift angle, peak alpha and lifetime (roughly ±20–30 %), plus a slow random wobble along its path. The two divider rims get random speeds, texture offsets and phase per load (CSS custom properties set once from JS), so the two dividers never move in sync. This means randomness in shape and motion, not a film-grain overlay.
- **Reduced motion:** no scroll smoke; the divider rim stays as a static texture (no flow), glow static.
- **F12 outcomes accepted by the PM (2026-09-26):** one generator in `src/smoke.ts` also builds a soft, pre-blurred "reach" mask tile (normalised per load); rim strips 36 px with the fade at zero by 82 %, measured reach ≈ 13–18 px mean (p10 8–12, p90 19–22), flow slants 18°/27°/34° left or right plus ±5–10 px sway; scroll-wisp peak alpha 0.18–0.28 per wisp (≤ 0.31 overlapping); one ≈ 150 ms long task at load for sprites + tiles.

### 4.4 Docker

`docker-compose.yml` (PM) — only `web` publishes a host port:

```yaml
services:
  db:
    image: postgres:17-alpine
    environment: { POSTGRES_USER: smoke, POSTGRES_PASSWORD: ${DB_PASSWORD}, POSTGRES_DB: smoke }
    volumes: [pgdata:/var/lib/postgresql/data]
    healthcheck: { test: ["CMD-SHELL", "pg_isready -U smoke -d smoke"], interval: 5s, timeout: 3s, retries: 10 }
  api:
    build: ./backend
    environment:
      ConnectionStrings__Default: Host=db;Database=smoke;Username=smoke;Password=${DB_PASSWORD}
      ADMIN_API_KEY: ${ADMIN_API_KEY}
    depends_on: { db: { condition: service_healthy } }
  web:
    build: ./frontend
    ports: ["80:80"]
    depends_on: [api]
volumes: { pgdata: {} }
```

`frontend/nginx.conf` (F1) — `proxy_pass` **without** trailing slash so `/api/...` is forwarded unchanged:

```nginx
server {
  listen 80;
  root /usr/share/nginx/html;
  gzip on; gzip_types text/css application/javascript application/json image/svg+xml;
  location /api/ { proxy_pass http://api:8080; proxy_set_header Host $host; }
  location / { try_files $uri $uri/ /index.html; }
}
```

Dockerfiles are standard two-stage builds. Frontend: `node:24-alpine` → `npm ci && npm run build` → `nginx:alpine` with `nginx.conf` at `/etc/nginx/conf.d/default.conf` and `dist/` at `/usr/share/nginx/html`. Backend: `mcr.microsoft.com/dotnet/sdk:10.0` → `dotnet publish -c Release -o /out` → `mcr.microsoft.com/dotnet/aspnet:10.0`, `ENTRYPOINT ["dotnet","SmokeMkk.Api.dll"]`. Do **not** set `ASPNETCORE_URLS`; the image listens on 8080.

Local dev without the full stack: `docker compose up db -d`, then `dotnet run --project backend` (port 5000) and `npm run dev --prefix frontend -- --port 5174` (proxies `/api`).

## 5. API contract — the bytes both specialists implement

Owned by the PM. `frontend/src/api.ts` mirrors these types verbatim; `backend/Program.cs` defines them as `record`s. JSON is camelCase, enums are strings, dates are ISO-8601 UTC strings.

```ts
export type Category = "Vape" | "Tobacco" | "Accessory" | "Drink" | "Snack";

export interface Machine {
  id: number;
  slug: string;
  name: string;
  street: string;        // "" when unknown (#7)
  postalCode: string;
  city: string;
  lat: number;
  lng: number;
  googleMapsUrl: string | null;
}

export interface InventoryItem {
  productId: number;
  name: string;
  category: Category;
  imageUrl: string | null;   // site-relative, e.g. "/products/red-bull-250.webp"; file may not exist yet
  quantity: number;      // >= 0
  priceCents: number;    // >= 0
}

export interface Inventory {
  machineId: number;
  updatedAt: string | null;   // max(updated_at) over items; null when no items
  items: InventoryItem[];     // sorted by category (Vape, Tobacco, Accessory, Drink, Snack), then name
}

export interface InventoryWrite {   // PUT body element
  productId: number;
  quantity: number;
  priceCents: number;
}
```

| Route | Auth | Success | Errors |
|---|---|---|---|
| `GET /api/machines` | none | `200 Machine[]` — active machines only, sorted by `name`; cached 60 s | — |
| `GET /api/machines/{id}/inventory` | none | `200 Inventory` | `404` machine missing or inactive |
| `PUT /api/machines/{id}/inventory` | header `X-Api-Key` | `204` — **replaces** the machine's whole stock list with the body | `400` ProblemDetails: unknown `productId`, duplicate `productId`, `quantity < 0`, `priceCents < 0`, missing field, malformed JSON, zero-length body; `[]` is allowed and clears the stock · `401` missing/wrong key · `404` machine missing (inactive machines are accepted) · `503` `ADMIN_API_KEY` not configured. Checked in the order 503 → 401 → 404 → 400. |
| `GET /health` | none | `200` text `ok` | — |

Errors use ASP.NET's default `ProblemDetails` body. The frontend shows §6 copy, never the server message.

## 6. German UI copy — decided once, by the PM

Informal *du*. No exclamation marks except the hero and rows marked *owner's wording*. Name the outcome, not the mechanism.

**Copy rules (marketing pass, owner request 2026-09-25).** Short and concrete; real place names over "deine Region". None of the stock phrases that read as machine-written (`Entdecke`, `Erlebe`, `Tauche ein`, `Willkommen bei`, `Wir bieten dir`, `nahtlos`, `perfekt`, `Genuss`), no three-adjective stacks, no `Egal ob … oder …`, no emojis. Never call the stock "live": it is maintained by hand and the panel shows its `Stand`. **Tabakerzeugnisgesetz §19(3), §21:** online advertising for tobacco and e-cigarettes is banned, as is copy that plays down health risks or appeals to minors. So the pitch sells the service (open round the clock, the locations, checking stock before the walk); vapes and tobacco appear only as the factual range and product names. Owner to have this checked before launch. Payment methods and the machines' own age check are good copy once the owner confirms the facts.

| Key | Text |
|---|---|
| html title / og:title | `Smoke MKK - Vapes, Drinks, Snacks & More!` (owner's wording, 2026-09-25; knowingly English and with an exclamation mark) |
| meta description / og:description | `Vapes, Drinks und Snacks aus dem Automaten, rund um die Uhr. Alle Standorte zwischen Rodgau und Fulda, mit Bestand auf der Karte.` |
| hero.words | `Vapes` · `Tabak` · `Rauchzubehör` · `Drinks` · `Snacks` (FlipWords; all five categories in §5 order, same labels as `category.*`; the longest word must fit at 375 px without breaking or overflowing) |
| hero.tagline | `{n}x im Main-Kinzig-Kreis und Umgebung` (owner's wording; n = active machines, computed, never hard-coded; `1x` needs no singular form) |
| hero.cta.map | `Automat in deiner Nähe finden` (owner's wording; primary button, first hero CTA, jumps to `#standorte`) |
| hero.social.lead | `Folg uns, um nichts zu verpassen!` (owner's wording; small line directly above the Instagram/TikTok CTAs) |
| hero.cta.instagram / tiktok | `Instagram` · `TikTok` |
| pitch.title | `Spät dran? Wir nicht.` (display type, uppercase like the other headings) |
| pitch.body | `Tanke zu, Kiosk zu, Kühlschrank leer? Unsere Automaten haben trotzdem auf. Kalte Drinks, Snacks und der Rest vom Sortiment, auch sonntags und nachts um drei.` |
| section.locations | `Unsere Automaten` (owner's wording; the anchor stays `#standorte`) |
| section.locations.lead | `Hast du Lust auf Snacks, Drinks and more?` (emphasised) `Such dir einen Automaten in deiner Nähe aus und schau, ob deine Lieblingssachen verfügbar sind!` (owner's wording; one paragraph under the heading) |
| panel.title | `Alle Automaten` (owner, 2026-09-25; was `Aktueller Bestand`, which misled above the machine list). The `<h3>` of the panel's list view only; in the detail view the `panel.back` button carries the same words and the machine's display name is the `<h3>` |
| panel.empty | `Such dir einen Automaten aus. Hier steht dann, was drin ist.` |
| panel.noStock | `Für diesen Automaten ist noch kein Bestand hinterlegt.` |
| panel.error | `Der Bestand lädt gerade nicht. Versuch's gleich noch mal.` |
| panel.updatedAt | `Stand: {datetime}` |
| panel.summary | `{n} Produkte · {m} Artikel im Automaten` (n = items with quantity > 0, m = sum of quantities); singular `1 Produkt` / `1 Artikel`; hidden when the machine has no items |
| skipLink | `Zum Inhalt springen` (first focusable element, visible on focus, targets `<main>`) |
| logo.alt | `SMOKE` |
| panel.quantity | `{qty} Stück` (0 → `0 Stück`, shown next to the `ausverkauft` badge) |
| stock.available / low / soldOut | `verfügbar` (qty > 3) · `fast weg` (1–3) · `ausverkauft` (0) |
| category.Vape / Tobacco / Accessory / Drink / Snack | `Vapes` · `Tabak` · `Rauchzubehör` · `Drinks` · `Snacks` |
| panel.route | `Route` (link in the panel header of the selected machine; was `card.route` until F10) |
| panel.back | `Alle Automaten` (button at the top of the panel while a machine is selected; returns to the list) |
| search.label | `PLZ oder Ort` (visible label of the panel's search field) |
| search.placeholder | `z. B. 63607 oder Wächtersbach` |
| search.noResults | `Da steht noch keiner. Schau auf der Karte, welcher Automat am nächsten ist.` |
| search.count | `{n} Automaten` (singular `1 Automat`; screen-reader status only) |
| geo.nearest | `Am nächsten` (badge on the nearest machine once the location is known) |
| geo.you | `Dein Standort` (the visitor's dot on the map) |
| geo.distance | `{km} km` (one decimal, `de-DE`, e.g. `3,2 km`; straight line) |
| header.locations | `Standorte` (header link to `/#standorte`, on every page) |
| machine display name | the machine `name` without its leading `SMOKE ` (e.g. `Fulda`), wherever a machine is named in the UI; the header carries the brand |
| map.loadError | `Die Standorte konnten nicht geladen werden.` |
| age.title | `Bist du 18 oder älter?` |
| age.body | `Hier geht's auch um Vapes und Tabak. Die gibt's erst ab 18.` |
| age.yes / age.no | `Ja, ich bin 18+` · `Nein, noch nicht` |
| age.denied | `Dann ist die Seite noch nichts für dich.` |
| footer.claim | `© {year} Smoke MKK - Vapes, Drinks, Snacks & More!` (owner's wording, 2026-09-25, same spelling as the site title; `{year}` = current year, computed; one muted line in the footer, above or beside the legal links) |
| footer.imprint / privacy | `Impressum` · `Datenschutz` |
| legal.imprint.title / privacy.title | `Impressum` · `Datenschutzerklärung` |
| legal.todo | `TODO: Text vom Betreiber einfügen.` |

Inactive machines are never rendered. New strings not in this table are the PM's to add here first.

## 7. Verification — the PM's gate

Runs once, by the PM, after both specialists have reported. A specialist's own build is useful but is not this.

1. `docker compose up --build -d` → all three services healthy/running (`docker compose ps`).
2. `curl -s http://localhost/health` → `ok`.
3. `curl -s http://localhost/api/machines | jq length` → `11`.
4. `curl -s http://localhost/api/machines/1/inventory | jq '.items | length'` → `9`; `updatedAt` non-null.
5. `curl -i -X PUT -H "X-Api-Key: $ADMIN_API_KEY" -H "Content-Type: application/json" -d '[{"productId":1,"quantity":0,"priceCents":1290}]' http://localhost/api/machines/1/inventory` → `204`; the inventory now has 1 item with quantity 0. Same call without key → `401`; with `productId: 999` → `400`.
6. Browser at http://localhost: 18+ dialog appears; "Ja" closes it and it stays closed after reload; "Nein" shows the denial and blocks. With the flag cleared, `/impressum` loads without the dialog, and the dialog's legal links work in both states.
7. Smoke intro dissolves on load (holds behind the 18+ dialog until "Ja"); absent under reduced motion. Hero: aurora backdrop, FlipWords cycling; with OS reduce-motion on, words render statically and inventory items appear without animation.
8. Map: 11 brand-coloured markers, no broken images, all inside the initial viewport; click marker → map zooms onto it, panel shows name, address, Route, "Stand" and grouped items with the three stock states; pick a machine in the panel list → same; `Alle Automaten` → back to the list and the overview.
9. `docker compose down` then `docker compose up -d` → data persists (volume), seed adds no duplicate rows (`jq length` still `11`).
10. 375 px width: hero, map, panel stack; no horizontal scroll; a one-finger swipe over the map scrolls the page until the map is tapped. Keyboard: dialog buttons, markers, panel list and footer links reachable; focus visible.
11. Console clean (no errors) on `/`, `/impressum`, `/datenschutz`; deep-link reload on `/impressum` works (nginx SPA fallback).

## 8. Prerequisites on this machine

- **Docker Desktop 4.92 is installed** (per user, `%LOCALAPPDATA%/Programs/DockerDesktop`), but **WSL is not installed**, so its engine cannot start ("no virtualization"). Fix, as admin: `wsl --install`, then reboot — **user action**. A shell opened before the Docker install lacks it on PATH; open a new one. Everything up to `dotnet build` / `npm run build` works without it.
- Present: Node 24, npm 11, .NET 10 SDK, git. `dotnet-ef` is a local tool in `backend/dotnet-tools.json`: run `dotnet tool restore` inside `backend/`, then `dotnet ef`.
- **A Postgres server already listens on 127.0.0.1:5432 on this machine** (not ours). Never publish the compose `db` on host port 5432; use 5433 for local API development.
- The `agent-skills` plugin loads on the next session start (enabled in `.claude/settings.json`); `ui-ux-pro-max` is already in `.claude/skills/`.

## 9. Tickets and ownership

| ID | Owner | Scope (paths) | Done when | Depends on |
|---|---|---|---|---|
| **T0** | PM | root: `git init`, `.gitignore`, `.env.example` (`DB_PASSWORD`, `ADMIN_API_KEY`), `README.md`, `docker-compose.yml` | files exist, first commit made | — |
| **B1** | smokemkk-backend | `backend/**` — scaffold, `Db.cs`, `Seed.cs` (§3), endpoints (§5), `launchSettings.json` port 5000, `Migrations/Init`, `Dockerfile`, `.dockerignore` | `dotnet build -warnaserror` clean; report lists every route with its status codes | §5 |
| **F1** | smokemkk-frontend | `frontend/**` — `ui-ux-pro-max` first (palette + fonts), scaffold, Tailwind, Inspira prerequisites + tokens, 4 copied components, 5 own components, 2 views, router, `api.ts`, `index.html` meta, `public/`, `nginx.conf`, `Dockerfile`, `.dockerignore` | `npm run build` clean (incl. `vue-tsc`); browser check at 1280×800 and 375×812 with the dev server (API may be absent → §6 empty/error states must render) | §5, §6 |
| **B2** | smokemkk-backend | `backend/**` — `Product.ImageUrl` (nullable), second migration `AddProductImage`, seed catalogue renamed with image paths per §3, `imageUrl` in `InventoryItemDto` per §5 | `dotnet build -warnaserror` clean; `has-pending-model-changes` none; exactly two migrations | B1, §5 |
| **F1+** | smokemkk-frontend | added to F1: product images with category fallback, quantity per item, panel summary (§4.3, §6); `public/products/` stays empty apart from a `README.txt` naming the expected files | as F1, plus the panel checked with the API absent (§6 states) | §5, §6 |
| **F2** | smokemkk-frontend | `frontend/**` — self-host Space Grotesk (`public/fonts/*.woff2`, `@font-face`, `font-display: swap`, remove every fonts.googleapis.com / fonts.gstatic.com reference); `skipLink` per §6; `panel.summary` singular forms per §6 | `npm run build` clean; `grep -r googleapis frontend/src frontend/index.html` empty; browser network tab shows no request to a Google host | F1 |
| **B3** | smokemkk-backend | `backend/**` — `Category` enum gains `Tobacco`, `Accessory` in the §4.2 order; seed per §3 (id 3 renamed, ids 10–13 added, stock rows for all 13 on every active machine, all three stock states still on every machine) | `dotnet build -warnaserror` clean; `has-pending-model-changes` none (enum stored as text → no migration expected; if EF wants one, stop and report) | B2, §5 |
| **F3** | smokemkk-frontend | `frontend/**` — mirror the §5 `Category` type; §6 labels; two new placeholder SVGs (tobacco, lighter); category order from §5; `public/products/README.txt` lists all 13 files | `npm run build` clean; panel shows five groups in order against the running API | §5, §6 |
| **F4** | smokemkk-frontend | `frontend/**` — marker drop-in per §4.3 "Marker drop-in"; 13 placeholder illustrations per §3 "Placeholder product illustrations": one Pillow script in `frontend/scripts/product-art/`, outputs at the §3 file names in `public/products/` | 13 transparent 400×400 WebP files; panel shows them against the live API at 1280×800 and 375×812; sold-out greyscale still works; `npm run build` clean | F3 |
| **F5** | smokemkk-frontend | `frontend/**` — `ui-ux-pro-max` first; apply the marketing pass in §6: changed strings (title/og, meta/og description, `hero.tagline` incl. singular, `panel.empty`, `panel.error`, `stock.low`, `age.body`, `age.no`, `age.denied`) and new ones (`hero.cta.map` button, `hero.social.lead`, `section.locations.lead`, `footer.claim`); no new component, no new dependency | `npm run build` clean; `grep -rn "wenige\|Wähle einen\|Zutritt ab" frontend/src frontend/index.html` empty; browser check at 1280×800 and 375×812: button jumps to the map and keeps focus order sane, new lines readable (≥ 4.5:1), no horizontal scroll | §6 |
| **F6** | smokemkk-frontend | `frontend/src/router.ts`, `frontend/src/assets/main.css`, `frontend/src/views/HomeView.vue` (comment only) — two a11y fixes found in F5: (1) the Leaflet container's keyboard focus ring is clipped by the rounded `overflow-hidden` map wrapper → `.leaflet-container:focus-visible` gets an inset outline (`outline-offset: -2px`) in the existing focus-ring token; (2) `scrollBehavior` always returns `{ top: 0 }`, so hash links (skip link, `#standorte`) snap to the top → return `savedPosition` when present, else for `to.hash` scroll to that element with `top: 80` (64 px sticky header + gap; the router ignores `scroll-margin`), `behavior: 'smooth'` only when `prefers-reduced-motion` is not `reduce`, else `{ top: 0 }`. Keep the "Automat finden" click handler (it moves focus); update its now-stale comment | `npm run build` clean; browser 1280×800: Tab into the map shows a visible ring on all four sides; skip link and `/#standorte` (direct load and click) land with the target below the header; `/impressum` → `/` still starts at the top; back button restores position | F5 |
| **F7** | smokemkk-frontend | `frontend/index.html`, `frontend/src/views/HomeView.vue` — `<title>`/`og:title` per §6; `hero.words` = all five categories per §6 (FlipWords, the sr-only text and the reduced-motion static line); size the headline so `RAUCHZUBEHÖR` fits on one line at 375 px with no layout jump between words; reserve the `hero.tagline` line height before the API answers, so a direct load of `/#standorte` is not pushed down after the scroll (F6 finding) | `npm run build` clean; browser at 1280×800 and 375×812: all five words cycle, no overflow or wrap, hero height stable while words change; direct load of `/#standorte` puts the heading below the header | F6 |
| **F8** | smokemkk-frontend | `frontend/src/views/HomeView.vue`, `frontend/src/components/SocialLinks.vue` (stale comment only) — owner's copy per §6: `hero.tagline`, `hero.cta.map`, `hero.social.lead`, `section.locations`, `section.locations.lead` (first sentence emphasised as before); keep the `#standorte` id | `npm run build` clean; strings match §6 byte for byte; 375×812: the longer button label wraps or fits cleanly, stays ≥ 44 px tall, no horizontal scroll; 1280×800 unchanged layout | F7 |
| **F9** | smokemkk-frontend | `frontend/src/views/HomeView.vue` — `ui-ux-pro-max` first; new pitch band between the hero and `#standorte`: `pitch.title` + `pitch.body` per §6, centred, max ~40rem text width, generous vertical space, one `Reveal`; styled from existing tokens (e.g. subtle violet glow or a thin gradient divider), no image, no icon grid, no new component or dependency; an `<h2>` with its own `aria-labelledby` section | `npm run build` clean; 1280×800 and 375×812: reads as a clear break between hero and map, body ≥ 4.5:1, no horizontal scroll; hash jump to `#standorte` still lands below the header | F8 |
| **F10** | smokemkk-frontend | `frontend/**` — `ui-ux-pro-max` first; the UX-audit changes in §4.3 "UX audit changes (F10)" and the new §6 strings (`header.locations`, `panel.back`, `panel.route`, `search.*`, `geo.*`, display name): `App.vue` (header link, footer claim), `AgeGate.vue`, `MachineMap.vue`, `InventoryPanel.vue`, `HomeView.vue`, `Reveal.vue`, `api.ts` (display-name helper only, contract types untouched), `main.css` (tooltip + touch zoom buttons), `LegalView.vue` (TODO comment gains geolocation), `public/logo.webp`; delete `MachineCard.vue`, `ui/CardSpotlight.vue`, `ui/BorderBeam.vue`. No new dependency. | `npm run build` clean; no reference to the deleted files; browser against the running API at 1280×800 and 375×812: `/impressum` with the age flag cleared shows no dialog and `/` then shows it; markers show name tooltips, selecting zooms in, `Alle Automaten` zooms back out; panel list → detail → back works by mouse and keyboard; "Stand" without seconds under the summary; sold-out items last per category; panel heading `Alle Automaten` in the list view, no `Aktueller Bestand` anywhere; footer reads `© 2026 Smoke MKK - Vapes, Drinks, Snacks & More!`; search: `63607` → Wächtersbach + Hesseldorf, `schluchtern` → Schlüchtern, `xyz` → `search.noResults`, query kept after detail → back; location granted (emulate a position near Wächtersbach): your dot on the map, Wächtersbach marked `Am nächsten` on map and in the list, list sorted by distance with km, reload with permission granted marks without prompting or scrolling; at 375 a selection brings the panel into view and one-finger swipe over the untapped map scrolls the page; no horizontal scroll; hero + pitch band fully visible on the first screen, heights ≈ 60 % / 40 % of it (measured), and the map just below the fold at 1280×800 and 375×812, caret at the bottom of that screen jumps to `#standorte` (below the header, section focused, no location prompt), fades out on click and stays hidden (not focusable) while `#standorte` is reached, back when scrolled to the top; both dividers (hero ↔ pitch, pitch ↔ `#standorte`) glow and pulse in the same style (static under reduced motion); order hero → pitch band → map + panel, map section last before the footer; console clean | F9 |
| **F11** | smokemkk-frontend | `frontend/src/components/SmokeIntro.vue` (new), `frontend/src/App.vue`, `frontend/src/components/AgeGate.vue`, `frontend/src/assets/main.css` if needed — `ui-ux-pro-max` first; the smoke intro per §4.3 "Smoke intro". No new dependency, no new string. | `npm run build` clean; browser at 1280×800 and 375×812: confirmed visitor → smoke dissolves on load in ≤ 1.6 s, then the layer is gone from the DOM and no rAF keeps running; flag cleared → smoke stays behind the dialog, "Ja" dissolves it, "Nein" keeps it; `/impressum` with the flag cleared → dissolves at once, no dialog; SPA navigation does not replay it; reduced motion → no smoke at all; clicks and Tab work during the dissolve; console clean | F10 |
| **F12** | smokemkk-frontend | `frontend/src/components/SmokeIntro.vue`, new shared smoke module + scroll-smoke component under `frontend/src/`, `App.vue` (mount), `HomeView.vue` (divider markup if needed), `main.css` — `ui-ux-pro-max` first; §4.3 "Scroll smoke and smoking dividers". No new dependency, no new string. | `npm run build` clean; one texture generator in the tree; browser at 1280×800 and 375×812: scrolling spawns faint edge wisps that fade, none over the text column, no rAF/timers when idle; both dividers carry a thin smoke rim above and below, ~16 px on average with a random, ragged, slowly shifting reach (≈ 8–26 px) that flows outward from the line (no puffs, no wide band), no horizontal scroll; intro unchanged; reduced motion → neither effect; console clean | F11 |
| **F13** | smokemkk-frontend | `frontend/src/views/HomeView.vue` — `ui-ux-pro-max` first; §4.3 "Headline down to the CTA, bigger". No new string. | `npm run build` clean; 1280×800 and 375×812: logo at the top, headline + tagline sit just above the button as one centred group, headline visibly bigger (report the px size at both widths), `RAUCHZUBEHÖR` on one line at 320 and 375 px, no layout jump while words swap, hero + pitch still fill the first screen at ≈ 60/40 without clipping, no horizontal scroll | F12 |
| **F14** | smokemkk-frontend | `frontend/src/views/LegalView.vue` — the `DATENSCHUTZERKLÄRUNG` heading (30 px, 357 px wide) overflows: 14 px into the gutter at 375, page scrolls sideways at 320 (found in F13) → smaller on phones and/or `hyphens-auto break-words` (`lang="de"` is set) | `npm run build` clean; no horizontal scroll on `/impressum`, `/datenschutz` at 320 and 375 | — |
| **T2** | PM | run §7; legal placeholder review; visually compare all 12 pins with the Google links; commit; update this file's status line | §7 all green | B1, F1, Docker Desktop |

B1 and F1 run **in parallel** — disjoint paths, contract already fixed. The migration is created once, in B1, by the backend specialist; nobody else runs `dotnet ef`.

## 10. Deliberately skipped — add when …

- Watermelon blocks beyond visual reference → port one with motion-v when a specific block is wanted.
- shadcn-vue → when forms/menus/tables appear (phase 2 admin, phase 3 shop).
- Iconify → when more than a handful of icons are needed (Instagram/TikTok are inline SVGs).
- Light theme toggle → never asked for; tokens already carry the values.
- `Product.PriceCents`/VAT → phase 3 (trivial migration). (`Product.ImageUrl` was pulled into phase 1 on 2026-09-25.)
- Image upload/admin → phase 2; phase 1 images are files the owner drops into `frontend/public/products/`.
- Pinia / shared state → when a second view needs the machine list.
- Marker clustering → when > ~50 machines.
- Admin UI → phase 2; PUT + curl covers phase 1.
- Tests beyond §7 → when the PUT grows a second rule; then one xUnit project, WebApplicationFactory, Testcontainers.
- Read-only scout/build worker agents (CareFlow has them) → when the tree is large enough that specialists wait on searches.
- i18n → if English is ever requested.
- Real age verification, auth beyond the static API key → phase 3.
- CI, TLS reverse proxy (Caddy in front of `web`, ~5 lines), pre-rendering for SEO → at deployment.

## 11. Roadmap (outline only)

| Phase | Scope |
|---|---|
| 2 | Inventory upkeep: tiny admin page or a telemetry polling job (depends on what the machines expose); low-stock badge |
| 3 | B2C shop: catalogue, cart, checkout (Stripe/PayPal), real age verification (legally required for vapes), order e-mails; adds `customer`, `order`, `order_line` |
| 4 | B2B: business accounts, net price lists, invoice payment, bulk orders; adds `price_list` |

The phase-1 model already separates `product` from `machine_inventory`, so the shop reuses `product`. Nothing else is pre-built.
