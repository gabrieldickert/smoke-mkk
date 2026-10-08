# Smoke MKK

Website and store finder for Smoke MKK's vending machines (vapes, drinks, snacks) in and around the Main-Kinzig-Kreis.
Vue 3 SPA with an OpenStreetMap map, ASP.NET Core API, PostgreSQL, all in Docker.

Plan and rules: [`docs/PLAN.md`](docs/PLAN.md). Agent setup: [`CLAUDE.md`](CLAUDE.md).

## Run

Requires Docker Desktop.

```powershell
Copy-Item .env.example .env   # then set DB_PASSWORD and VENDON_API_KEY
docker compose up --build -d
```

Open http://localhost.

## Run without Docker (Windows)

One script starts everything, using your installed PostgreSQL (16 or newer) for a separate project database on port 5433 in the git-ignored `.localdb/` folder. The PostgreSQL service on 5432 is not touched.

```powershell
.\scripts\dev.ps1
```

On the first run it creates `.env` with random secrets and the database. Then it starts the API (http://localhost:5000) and the site (http://localhost:5174) in their own windows, and opens the site once both answer. Running it again while everything is up changes nothing.

```powershell
.\scripts\dev.ps1 -Stop
```

If PowerShell refuses to run scripts, use `powershell -ExecutionPolicy Bypass -File .\scripts\dev.ps1`.

## Local development

```powershell
docker compose up db -d
dotnet run --project backend          # http://localhost:5000
npm run dev --prefix frontend -- --port 5174   # http://localhost:5174, proxies /api
```

For `dotnet run`, set the connection string and the Vendon key in your shell, for example:

```powershell
$env:ConnectionStrings__Default = "Host=localhost;Database=smoke;Username=smoke;Password=<DB_PASSWORD>"
$env:VENDON_API_KEY = "<VENDON_API_KEY>"
```

`docker compose up db -d` does not publish a host port. To run the API outside Docker, publish the database on host port 5433 with a local override, and use `Port=5433` in the connection string. Port 5432 is already taken by another Postgres on the dev machine.

Migrations: `dotnet tool restore` inside `backend/`, then `dotnet ef migrations add <Name>`.

## Stock

Stock, names and prices come live from the Vendon Cloud API: the API polls `machine/{vendonId}/products` for every machine with the server-side `VENDON_API_KEY` every `VENDON_POLL_SECONDS` (default 60) and serves the last list it fetched. Edit stock and prices in Vendon Cloud; the site follows within a minute. Nothing is stored in the database apart from the sites and machines (`GET /api/locations`); the machine id is the Vendon device number (`[32]` in Vendon is `/api/machines/32/inventory`).

Categories are derived from the product name by a keyword rule (`docs/PLAN.md` §4.2); anything unmatched shows under "Sonstiges". The site has no product images yet (`docs/PLAN.md` §10).

## Live server

See `docs/PLAN.md` §8 "Live server": published API under systemd, static frontend under nginx, no Docker. Deploy = build locally, upload with `pscp`, swap the folders, restart `smokemkk-api`.

## Open items for the owner

- TODO: product photos for the 9 seeded products (see "Product images").
- TODO: Impressum and Datenschutz texts.
