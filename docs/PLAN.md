# Smoke MKK — Phase 1 implementation plan

*Status: approved 2026-09-25 · Owner: the PM/orchestrator (`.claude/agents/smokemkk-pm.md`) · Tree state: docs and agent definitions only, no code yet.*

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

**Placeholder catalogue** (real catalogue not public — owner replaces via `PUT`; mark in README as `TODO`):

| id | name | category |
|---|---|---|
| 1 | Einweg-Vape Blueberry Ice (600 Züge) | Vape |
| 2 | Einweg-Vape Watermelon (600 Züge) | Vape |
| 3 | Nikotin-Pouches Mint | Vape |
| 4 | Energy Drink 250 ml | Drink |
| 5 | Cola 330 ml | Drink |
| 6 | Wasser still 500 ml | Drink |
| 7 | Schokoriegel | Snack |
| 8 | Fruchtgummi 100 g | Snack |
| 9 | Chips Paprika 50 g | Snack |

Seed stock: every active machine gets all 9 products; quantities vary so that all three display states (§6) appear on every machine (e.g. `7, 5, 0, 12, 2, 8, 6, 0, 3`), `price_cents` plausible (vapes 899–1299, drinks 250–350, snacks 150–250), `updated_at` = seed time.

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
      components/InventoryPanel.vue
      components/MachineCard.vue
      views/HomeView.vue
      views/LegalView.vue
```

### 4.2 Backend — ASP.NET Core minimal API + EF Core + Npgsql

Entities (EF defaults, no naming plugin):

- `Machine(Id, Slug, Name, Street, PostalCode, City, Lat, Lng, IsActive, GoogleMapsUrl)` — `Slug` unique
- `Product(Id, Name, Category)` — `Category` is a C# enum `Vape | Drink | Snack` stored as string (`HasConversion<string>()`), so a later age filter is a WHERE clause
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
| `FlipWords` | hero headline cycling `Vapes · Drinks · Snacks` | text-animations |
| `CardSpotlight` | one card per machine in the grid | cards |
| `BorderBeam` | accent on the inventory panel while a machine is selected (optional — drop if it fights the map) | special-effects |

Own components:
- `MachineMap.vue` — raw Leaflet in `onMounted` on a `ref` div, destroyed in `onUnmounted`. Tiles `https://tile.openstreetmap.org/{z}/{x}/{y}.png` with attribution `&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>-Mitwirkende` (mandatory; swap to CARTO Voyager is a one-line URL change if traffic grows). Markers via `L.divIcon` with a Tailwind-styled class (avoids Leaflet's broken default-icon paths under Vite). `fitBounds` over all machines with padding. Props `machines`, `selectedId`; emits `select(id)`. Selected marker gets a distinct class.
- `InventoryPanel.vue` — prop `machine | null`; loads inventory on change; groups by category; stock state per §6 thresholds (one const); price `(priceCents/100).toLocaleString("de-DE", {style:"currency", currency:"EUR"})`; "Stand" from `updatedAt` via `toLocaleString("de-DE")`. Items fade in with `<motion.li>` from motion-v (`initial/animate`), guarded by `useReducedMotion` from motion-v — no animation when the OS asks for reduced motion.
- `MachineCard.vue` — wraps `CardSpotlight`; name, address, "Route" link (`google_maps_url`, `target="_blank" rel="noopener"`), click selects on the map and scrolls the map into view.
- `AgeGate.vue` — native `<dialog>`; `showModal()` in `onMounted` unless `localStorage.getItem("ageConfirmed") === "1"` (wrap storage access in try/catch). "Ja" → set flag, `close()`. "Nein" → swap content to the denial text, keep open. `Escape` disabled (`@cancel.prevent`). Tailwind only.
- `HomeView.vue` — `AuroraBackground` hero (logo, `FlipWords`, tagline, Instagram/TikTok CTAs) → section "Standorte": map left / `InventoryPanel` right (stacked below `md`) → `MachineCard` grid (the accessibility/SEO text fallback for the map). Empty/error states per §6.
- `LegalView.vue` — one component, `route.name` decides Impressum vs. Datenschutz; placeholder texts marked `TODO`; Datenschutz must mention OSM tile requests (IP to openstreetmap.org), the `localStorage` age flag, and the Instagram/TikTok links.
- SEO basics only: `lang="de"`, `<title>`, `meta description`, OG tags, `robots.txt` (allow all). No pre-rendering.

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

Local dev without the full stack: `docker compose up db -d`, then `dotnet run --project backend` (port 5000) and `npm run dev --prefix frontend` (port 5173, proxies `/api`).

## 5. API contract — the bytes both specialists implement

Owned by the PM. `frontend/src/api.ts` mirrors these types verbatim; `backend/Program.cs` defines them as `record`s. JSON is camelCase, enums are strings, dates are ISO-8601 UTC strings.

```ts
export type Category = "Vape" | "Drink" | "Snack";

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
  quantity: number;      // >= 0
  priceCents: number;    // >= 0
}

export interface Inventory {
  machineId: number;
  updatedAt: string | null;   // max(updated_at) over items; null when no items
  items: InventoryItem[];     // sorted by category (Vape, Drink, Snack), then name
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

Informal *du*. No exclamation marks except the hero. Name the outcome, not the mechanism.

| Key | Text |
|---|---|
| html title | `SMOKE – Vapes, Drinks & Snacks aus dem Automaten` |
| meta description | `Automaten für Vapes, Drinks und Snacks in Hessen – rund um die Uhr. Alle Standorte und der aktuelle Bestand auf einen Blick.` |
| hero.words | `Vapes` · `Drinks` · `Snacks` (FlipWords) |
| hero.tagline | `Rund um die Uhr. An {n} Standorten in Hessen.` (n = active machines, computed) |
| hero.cta.instagram / tiktok | `Instagram` · `TikTok` |
| section.locations | `Standorte` |
| panel.title | `Aktueller Bestand` |
| panel.empty | `Wähle einen Automaten auf der Karte oder in der Liste.` |
| panel.noStock | `Für diesen Automaten ist noch kein Bestand hinterlegt.` |
| panel.error | `Der Bestand konnte gerade nicht geladen werden. Versuch es gleich noch einmal.` |
| panel.updatedAt | `Stand: {datetime}` |
| stock.available / low / soldOut | `verfügbar` (qty > 3) · `wenige` (1–3) · `ausverkauft` (0) |
| category.Vape / Drink / Snack | `Vapes` · `Drinks` · `Snacks` |
| card.route | `Route` |
| map.loadError | `Die Standorte konnten nicht geladen werden.` |
| age.title | `Bist du 18 oder älter?` |
| age.body | `Diese Seite zeigt Produkte, die erst ab 18 Jahren erhältlich sind.` |
| age.yes / age.no | `Ja, ich bin 18+` · `Nein` |
| age.denied | `Zutritt ab 18 Jahren. Diese Seite ist für dich nicht zugänglich.` |
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
6. Browser at http://localhost: 18+ dialog appears; "Ja" closes it and it stays closed after reload; "Nein" shows the denial and blocks.
7. Hero: aurora backdrop, FlipWords cycling; with OS reduce-motion on, words render statically and inventory items appear without animation.
8. Map: 11 brand-coloured markers, no broken images, all inside the initial viewport; click marker → panel shows grouped items with the three stock states and "Stand"; click a card → same, map scrolls into view.
9. `docker compose down` then `docker compose up -d` → data persists (volume), seed adds no duplicate rows (`jq length` still `11`).
10. 375 px width: hero, map, panel, cards stack; no horizontal scroll. Keyboard: dialog buttons, cards and footer links reachable; focus visible.
11. Console clean (no errors) on `/`, `/impressum`, `/datenschutz`; deep-link reload on `/impressum` works (nginx SPA fallback).

## 8. Prerequisites on this machine

- **Docker Desktop is not installed** (checked 2026-09-25: not on PATH, no install dir; WSL present). Install with the WSL 2 backend before T2 — **user action**. Everything up to `dotnet build` / `npm run build` works without it.
- Present: Node 24, npm 11, .NET 10 SDK, git. `dotnet-ef` is a local tool in `backend/dotnet-tools.json`: run `dotnet tool restore` inside `backend/`, then `dotnet ef`.
- **A Postgres server already listens on 127.0.0.1:5432 on this machine** (not ours). Never publish the compose `db` on host port 5432; use 5433 for local API development.
- The `agent-skills` plugin loads on the next session start (enabled in `.claude/settings.json`); `ui-ux-pro-max` is already in `.claude/skills/`.

## 9. Tickets and ownership

| ID | Owner | Scope (paths) | Done when | Depends on |
|---|---|---|---|---|
| **T0** | PM | root: `git init`, `.gitignore`, `.env.example` (`DB_PASSWORD`, `ADMIN_API_KEY`), `README.md`, `docker-compose.yml` | files exist, first commit made | — |
| **B1** | smokemkk-backend | `backend/**` — scaffold, `Db.cs`, `Seed.cs` (§3), endpoints (§5), `launchSettings.json` port 5000, `Migrations/Init`, `Dockerfile`, `.dockerignore` | `dotnet build -warnaserror` clean; report lists every route with its status codes | §5 |
| **F1** | smokemkk-frontend | `frontend/**` — `ui-ux-pro-max` first (palette + fonts), scaffold, Tailwind, Inspira prerequisites + tokens, 4 copied components, 5 own components, 2 views, router, `api.ts`, `index.html` meta, `public/`, `nginx.conf`, `Dockerfile`, `.dockerignore` | `npm run build` clean (incl. `vue-tsc`); browser check at 1280×800 and 375×812 with the dev server (API may be absent → §6 empty/error states must render) | §5, §6 |
| **T2** | PM | run §7; legal placeholder review; visually compare all 12 pins with the Google links; commit; update this file's status line | §7 all green | B1, F1, Docker Desktop |

B1 and F1 run **in parallel** — disjoint paths, contract already fixed. The migration is created once, in B1, by the backend specialist; nobody else runs `dotnet ef`.

## 10. Deliberately skipped — add when …

- Watermelon blocks beyond visual reference → port one with motion-v when a specific block is wanted.
- shadcn-vue → when forms/menus/tables appear (phase 2 admin, phase 3 shop).
- Iconify → when more than a handful of icons are needed (Instagram/TikTok are inline SVGs).
- Light theme toggle → never asked for; tokens already carry the values.
- `Product.ImageUrl`, `Product.PriceCents`/VAT → phase 3 (trivial migration).
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
