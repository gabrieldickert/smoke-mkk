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

## Local development

```powershell
docker compose up db -d
dotnet run --project backend          # http://localhost:5000
npm run dev --prefix frontend         # http://localhost:5173, proxies /api
```

For `dotnet run`, set the connection string and key in your shell, for example:

```powershell
$env:ConnectionStrings__Default = "Host=localhost;Database=smoke;Username=smoke;Password=<DB_PASSWORD>"
$env:ADMIN_API_KEY = "<ADMIN_API_KEY>"
```

`docker compose up db -d` does not publish port 5432 by default; add a `ports: ["5432:5432"]` override locally if you run the API outside Docker.

## Update a machine's stock

The request replaces the machine's whole stock list.

```bash
curl -X PUT http://localhost/api/machines/1/inventory \
  -H "X-Api-Key: $ADMIN_API_KEY" \
  -H "Content-Type: application/json" \
  -d '[{"productId":1,"quantity":12,"priceCents":1290}]'
```

`204` on success, `400` for unknown or duplicate products or negative values, `401` for a wrong key.

## Open items for the owner

- TODO: real product catalogue (the seed is a placeholder).
- TODO: machine "Burgjoss" has no known address; it is hidden until confirmed.
- TODO: Impressum and Datenschutz texts.
