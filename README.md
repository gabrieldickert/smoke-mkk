# Smoke MKK

Website and store finder for Smoke MKK's vending machines (vapes, drinks, snacks) in Hessen.
Vue 3 SPA with an OpenStreetMap map, ASP.NET Core API, PostgreSQL, all in Docker.

Plan and rules: [`docs/PLAN.md`](docs/PLAN.md). Agent setup: [`CLAUDE.md`](CLAUDE.md).

## Run

Requires Docker Desktop.

```powershell
Copy-Item .env.example .env   # then set DB_PASSWORD and ADMIN_API_KEY
docker compose up --build -d
```

Open http://localhost.

## Run without Docker (Windows, local PostgreSQL 16)

Uses the installed `C:\Program Files\PostgreSQL\16` binaries to run a separate project database on port 5433, stored in the git-ignored `.localdb/` folder. The PostgreSQL service on 5432 is not touched.

Start the database (the cluster already exists after the first setup):

```bash
"/c/Program Files/PostgreSQL/16/bin/pg_ctl.exe" start -D .localdb -o "-p 5433 -c listen_addresses=localhost" -l .localdb/server.log
```

Start the API (Git Bash, from the repo root):

```bash
set -a; . ./.env; set +a; export ConnectionStrings__Default="Host=localhost;Port=5433;Database=smoke;Username=smoke;Password=$DB_PASSWORD"; dotnet run --project backend
```

Start the site, then open http://localhost:5174:

```bash
npm run dev --prefix frontend -- --port 5174
```

Stop the database with `pg_ctl.exe stop -D .localdb`. First-time setup of the cluster, if `.localdb/` is missing: `initdb.exe -D .localdb -U smoke -W -A scram-sha-256 -E UTF8 --locale=C`, then start it and run `createdb.exe -h localhost -p 5433 -U smoke smoke`, using the `DB_PASSWORD` from `.env`.

## Local development

```powershell
docker compose up db -d
dotnet run --project backend          # http://localhost:5000
npm run dev --prefix frontend -- --port 5174   # http://localhost:5174, proxies /api
```

For `dotnet run`, set the connection string and key in your shell, for example:

```powershell
$env:ConnectionStrings__Default = "Host=localhost;Database=smoke;Username=smoke;Password=<DB_PASSWORD>"
$env:ADMIN_API_KEY = "<ADMIN_API_KEY>"
```

`docker compose up db -d` does not publish a host port. To run the API outside Docker, publish the database on host port 5433 with a local override, and use `Port=5433` in the connection string. Port 5432 is already taken by another Postgres on the dev machine.

Migrations: `dotnet tool restore` inside `backend/`, then `dotnet ef migrations add <Name>`.

## Update a machine's stock

The request replaces the machine's whole stock list.

```bash
curl -X PUT http://localhost/api/machines/1/inventory \
  -H "X-Api-Key: $ADMIN_API_KEY" \
  -H "Content-Type: application/json" \
  -d '[{"productId":1,"quantity":12,"priceCents":1290}]'
```

`204` on success, `400` for unknown or duplicate products, negative values or a malformed body, `401` for a missing or wrong key, `503` when `ADMIN_API_KEY` is not set on the server. Send `[]` to clear a machine's stock.

## Product images

Each product has a fixed image path, listed in `docs/PLAN.md` §3 (for example `/products/red-bull-250.webp`). Save the photo as a square WebP, about 400×400, under that exact name in `frontend/public/products/`. Rebuild the web image (`docker compose up --build -d web`). Until a file exists, the site shows a category placeholder.

Use your own photos or packshots you have the rights to. Do not copy brand images from the web.

## Open items for the owner

- TODO: real product catalogue (the seed is a placeholder).
- TODO: product photos for the 9 seeded products (see "Product images").
- TODO: machine "Burgjoss" has no known address; it is hidden until confirmed.
- TODO: Impressum and Datenschutz texts.
