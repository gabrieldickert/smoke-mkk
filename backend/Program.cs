using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SmokeMkk.Api;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
builder.Services.AddDbContext<AppDb>(o => o.UseNpgsql(connectionString, n => n.EnableRetryOnFailure()));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOutputCache();
builder.Services.AddProblemDetails();

// Vendon Cloud API (docs/PLAN.md §3, §4.2). The key is the trust boundary (CLAUDE.md §5.2): it lives only in this
// header, is never logged, never put in a URL, never returned. Empty or unset = not configured → the route answers 503
// and the poller does not start.
var vendonKey = builder.Configuration["VENDON_API_KEY"];
var vendonConfigured = !string.IsNullOrWhiteSpace(vendonKey);
var pollSeconds = int.TryParse(builder.Configuration["VENDON_POLL_SECONDS"], out var s) ? Math.Max(10, s) : 60;
var vendonBaseUrl = builder.Configuration["VENDON_BASE_URL"];
if (string.IsNullOrWhiteSpace(vendonBaseUrl)) vendonBaseUrl = "https://cloud.vendon.net/rest/v1.9.0/";
if (!vendonBaseUrl.EndsWith('/')) vendonBaseUrl += "/";   // relative "machine/{id}/products" must append, not replace the last segment
// Registered even without the key so the poller's IHttpClientFactory always resolves; without the key the poller
// never starts and the client carries no header.
builder.Services.AddHttpClient("vendon", c =>
    {
        c.BaseAddress = new Uri(vendonBaseUrl);
        c.Timeout = TimeSpan.FromSeconds(10);
        if (vendonConfigured) c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", vendonKey);
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    })
    .RedactLoggedHeaders(["Authorization"]);   // the factory's handler logs headers at Trace; never the key, even then
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);   // else 4 Information lines per fetch, 39 fetches per pass

// The poller's dictionary (docs/PLAN.md §4.2 "Vendon poll"): our machine id → the last list fetched from Vendon.
var stock = new ConcurrentDictionary<int, InventoryDto>();
builder.Services.AddSingleton(stock);
if (vendonConfigured)
    builder.Services.AddHostedService(sp => new VendonPoller(sp.GetRequiredService<IServiceScopeFactory>(),
        sp.GetRequiredService<IHttpClientFactory>(), sp.GetRequiredService<ILogger<VendonPoller>>(),
        TimeSpan.FromSeconds(pollSeconds), stock));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseOutputCache();

// ponytail: migrate + seed in-process is fine for a single replica; move to a migration job if the API ever scales out.
// Runs before app.Run() starts the hosted services, so the poller's first pass sees a migrated, seeded table.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    // Migrate() opens its first connection outside EnableRetryOnFailure; run it inside the strategy so the
    // first-boot window (pg_isready up, database not yet accepting) is retried, then fails after ~6 attempts.
    db.Database.CreateExecutionStrategy().Execute(() => db.Database.Migrate());
    Seed.Run(db);
}

app.MapGet("/health", () => "ok");

// Locations with at least one active machine; inactive machines are left out of the nested list.
app.MapGet("/api/locations", (AppDb db) =>
        db.Locations
            .Where(l => l.Machines.Any(m => m.IsActive))
            .OrderBy(l => l.Name)
            .Select(l => new LocationDto(l.Id, l.Slug, l.Name, l.Street, l.PostalCode, l.City, l.Lat, l.Lng, l.GoogleMapsUrl,
                l.Machines.Where(m => m.IsActive).OrderBy(m => m.Label).ThenBy(m => m.Id)
                    .Select(m => new MachineDto(m.Id, m.Label, m.PictureUrl)).ToList()))
            .ToListAsync())
    .CacheOutput(p => p.Expire(TimeSpan.FromSeconds(60)));

// Live stock from the poller's dictionary (docs/PLAN.md §4.2 "Vendon poll", §5). Order: 404 machine → 503 key not configured
// → 503 nothing fetched yet for this machine → 200. No output cache here: the dictionary is the cache.
app.MapGet("/api/machines/{id:int}/inventory", async (int id, AppDb db) =>
{
    if (!await db.Machines.AnyAsync(m => m.Id == id && m.IsActive))
        return Results.Problem(statusCode: StatusCodes.Status404NotFound);
    if (!vendonConfigured)
        return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, detail: "Live stock is not configured.");
    if (!stock.TryGetValue(id, out var entry))
        return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, detail: "Stock not loaded yet.");
    return Results.Ok(entry);
});

// Product search over the poller's dictionary (docs/PLAN.md §4.2 "Product search", §5). No DB, no Vendon call, no cache.
// ponytail: linear scan over ≈ 39 × 60 strings per request, fine for a decade.
app.MapGet("/api/search", (string? q) =>
{
    var query = Vendon.Fold(q ?? "");
    if (query.Length is < 2 or > 60) return Results.Ok(new List<SearchHitDto>());
    var hits = stock.Values
        .Select(inv => new SearchHitDto(inv.MachineId,
            inv.Items.Where(i => i.Quantity > 0 && Vendon.Fold(i.Name).Contains(query, StringComparison.Ordinal))
                .Select(i => i.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).Take(5).ToList()))
        .Where(h => h.Products.Count > 0)
        .OrderBy(h => h.MachineId)
        .ToList();
    return Results.Ok(hits);
});

app.Run();

// docs/PLAN.md §5 — wire names are camelCase (System.Text.Json web defaults), enums as strings.
record LocationDto(int Id, string Slug, string Name, string Street, string PostalCode, string City, double Lat, double Lng, string? GoogleMapsUrl, List<MachineDto> Machines);
record MachineDto(int Id, string Label, string? PictureUrl);
record InventoryItemDto(int ProductId, string Name, Category Category, int Quantity, int PriceCents);
record InventoryDto(int MachineId, DateTime? UpdatedAt, List<InventoryItemDto> Items);
record SearchHitDto(int MachineId, List<string> Products);   // B9: a machine whose current stock matches a product search
