# Smoke MKK — project guide

Entry document for anyone (human or agent) working in this repository: what the project is, where the rules live, how work is split between agents, how to run and verify it. It deliberately does not repeat the plan — `docs/PLAN.md` is the constitution and this file says when to read which part.

If a request conflicts with a rule here or in `docs/PLAN.md`, say so and propose the alternative — do not silently deviate.

## 0. What this is

**Smoke MKK** operates 39 self-service vending machines at 24 sites (vapes, drinks, snacks) around the Main-Kinzig-Kreis, Hessen. This repository is their website: a **Vue 3 SPA with an OpenStreetMap store finder** — one marker per machine, click → that machine's current inventory — backed by a **minimal ASP.NET Core API** and **PostgreSQL**, all in **docker compose**. A B2C/B2B shop follows in later phases; only the data model anticipates it.

Phase 1 is being built now. The tree held only docs and agent definitions on 2026-09-25; `docs/PLAN.md` §9 says what exists.

Stack: C# / .NET 10 · Vue 3 + Vite + TypeScript · Tailwind 4 + Inspira UI + motion-v · Leaflet 1.9 · PostgreSQL 17 · nginx.

## 1. Where the rules live — read the right part first

| Touching | Read first | Skill to invoke |
|---|---|---|
| anything | `docs/PLAN.md` §1–§2 (context, decisions) | `agent-skills:using-agent-skills` when unsure which applies |
| `frontend/**` — screens, components, styles, layout, copy placement, review | `docs/PLAN.md` §4.3, §5, §6 | **`ui-ux-pro-max` — mandatory, first, every time**; then `agent-skills:frontend-ui-engineering`; browser checks: `agent-skills:browser-testing-with-devtools` |
| `backend/**` — endpoints, DTOs, EF, seed | `docs/PLAN.md` §4.2, §5, §3 | `agent-skills:api-and-interface-design`; the PUT: `agent-skills:security-and-hardening` |
| `docker-compose.yml`, Dockerfiles, `nginx.conf` | `docs/PLAN.md` §4.4 | — |
| planning tickets, reconciling reports, committing (PM) | `docs/PLAN.md` §7, §9 | `agent-skills:planning-and-task-breakdown`, `agent-skills:code-review-and-quality`, `agent-skills:git-workflow-and-versioning` |
| something broke | — | `agent-skills:debugging-and-error-recovery` |
| adding a dependency, a table, a route, a string | `docs/PLAN.md` §10 (skipped list) | — if it is there, it needs a PM decision written into the plan first |

Skills: `ui-ux-pro-max` lives in `.claude/skills/` (project); the `agent-skills:*` set comes from the **addyosmani/agent-skills** plugin enabled in `.claude/settings.json` (loads at session start). The agent definitions pin their skills in frontmatter and repeat them as the first action — a specialist that skipped its skill has skipped a rule.

`docs/PLAN.md` is the source of truth, not the code. A decision that changes it is written back **in the same change**, by the PM.

## 2. Working with agents

Three agents, defined in `.claude/agents/`. One orchestrator, two specialists, never several peers editing the same tree.

| Agent | Role | Owns (writes) | Never touches |
|---|---|---|---|
| `smokemkk-pm` | orchestrator / project manager | root files, `docker-compose.yml`, `docs/**`, `CLAUDE.md`, `.claude/**`, `README.md`; the **contract** (§5), the **German copy** (§6), the **verification run** (§7), git commits | `backend/**`, `frontend/**` |
| `smokemkk-backend` | C#/.NET, EF Core, Postgres, backend Dockerfile | `backend/**` | everything else |
| `smokemkk-frontend` | Vue, Tailwind, Inspira, Leaflet, nginx, frontend Dockerfile — **always through `ui-ux-pro-max`** | `frontend/**` | everything else |

The session that opens this repository **is the PM** unless it was spawned as a specialist. Adopt the role: read `docs/PLAN.md` §9, take a ticket, dispatch. `Explore` (built-in, read-only) is available to everyone for fan-out searching; there are no other workers — the tree is ~30 files and does not need them.

Rules:

1. **One writer per path.** The table above is the fence. Two agents in one file is a merge conflict with no merge tool in the loop.
2. **The contract comes before the parallelism.** `frontend/src/api.ts` mirrors the `record`s in `backend/Program.cs`, and both mirror `docs/PLAN.md` §5. Neither specialist invents a shape; a needed change is reported to the PM, who edits §5 first and then hands the same bytes to both.
3. **German copy is decided once**, in §6, by the PM. A specialist that needs a new string reports it; it does not coin it.
4. **Migrations are single-writer.** Only `smokemkk-backend` runs `dotnet ef migrations add`, one migration at a time, only when the ticket says so.
5. **Specialists report in a fixed shape** — *changed · contract · rules · verified · left undone* — so the PM can reconcile two reports without re-reading both halves of the tree. *Rules* names the plan section and the skills applied, plus any deviation; a deviation is the PM's decision, not the specialist's.
6. **Verification is never delegated.** Only the PM runs `docs/PLAN.md` §7, once, after both specialists have reported. A specialist's `dotnet build` or `npm run build` is useful and is not that.
7. **Dispatch flows downward only.** Specialists hold no `Agent` tool; the PM does. A specialist that finds work outside its fence stops and reports it.
8. **Ticket first.** Do not start a specialist against an unticketed change — the scope fence *is* the ticket.

Do not run two PM sessions against the same working tree; they share no context and both believe they are the only writer. If two lines of work must run at once, split along the backend/frontend boundary — which is the one split this repository is built for — or use a separate git worktree per session.

## 3. Layout

```
backend/     ASP.NET Core minimal API — Program.cs, Db.cs, Seed.cs, Migrations/, Dockerfile
frontend/    Vue 3 + Vite + TS — src/{main.ts,router.ts,api.ts,App.vue,components/,views/,assets/}, nginx.conf, Dockerfile
scripts/     dev.ps1 (start/stop without Docker)
docs/        PLAN.md (constitution) — later ADRs as docs/ADR-00N-topic.md
docker-compose.yml   db (postgres:17-alpine) · api (:8080 internal) · web (nginx, :80 published)
.env.example         DB_PASSWORD, ADMIN_API_KEY — copy to .env, never commit .env
.claude/agents/      smokemkk-pm.md, smokemkk-backend.md, smokemkk-frontend.md
.claude/skills/      ui-ux-pro-max (project skill)
.claude/settings.json  plugins: claude-seo, agent-skills
.claude/launch.json  web-dev (Vite :5174), api-dev (dotnet :5000) for the browser preview
```

## 4. Commands

```powershell
.\scripts\dev.ps1                      # whole stack without Docker (local Postgres :5433); -Stop to stop
docker compose up --build -d          # the whole stack → http://localhost
docker compose up db -d               # Postgres only, for local dev
dotnet run --project backend          # API on http://localhost:5000 (needs Postgres)
npm run dev --prefix frontend -- --port 5174   # Vite on http://localhost:5174, proxies /api → :5000
npm run build --prefix frontend       # includes vue-tsc typecheck
dotnet build backend -warnaserror
```

Operator workflow for stock (until phase 2): `PUT /api/machines/{id}/inventory` with header `X-Api-Key` — see `docs/PLAN.md` §5 and `README.md`.

**Docker Desktop is installed but cannot start until WSL is installed (`wsl --install` as admin, then reboot).** Until then: build both apps, but no runtime verification.

## 5. Non-negotiables

1. **German to people, English to developers.** UI copy, legal pages, and anything a visitor reads: German, informal *du*, from §6. Code, comments, commit messages, docs, logs: English.
2. **The PUT is a trust boundary.** Validate every field, compare the API key in constant time, return `503` when the key is not configured (never "open"), never log the key. This is the one place where phase 1 is not lazy.
3. **The skipped list (§10) is binding.** No Pinia, no vue-leaflet, no shadcn-vue, no Iconify, no test framework, no extra runtime dependency without a PM decision written into the plan. Vue + Tailwind + the listed packages cover phase 1.
4. **Frontend work goes through `ui-ux-pro-max`.** Every frontend ticket, not only the first — the skill is the design method, not a one-off palette picker.
5. **Legal floor.** `Impressum` and `Datenschutz` reachable from every page, the age gate included (it stays closed on the legal pages); visible OpenStreetMap attribution on the map; the 18+ dialog on first visit. These are not polish.
6. **Accessibility floor.** Native `<dialog>`, keyboard-reachable markers/list/links, visible focus, ≥ 4.5:1 text contrast on the dark palette, animations off under `prefers-reduced-motion`. The machine list in the inventory panel is the text fallback for the map.
7. **Secrets stay out of git.** `.env` is ignored; `.env.example` carries names only. Only `web` publishes a host port.
8. **Seed never overwrites.** Startup seeding runs only on an empty `Machines` table; operator edits survive every restart and migration. `HasData` is banned for this reason.
9. **Minimal by design.** Records next to endpoints, one `Program.cs`, raw Leaflet in one component, plain fetch. A deliberate corner with a known ceiling carries a `// ponytail:` comment naming the ceiling and the upgrade path.
