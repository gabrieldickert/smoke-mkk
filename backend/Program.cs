using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SmokeMkk.Api;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
builder.Services.AddDbContext<AppDb>(o => o.UseNpgsql(connectionString, n => n.EnableRetryOnFailure()));
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    // A PUT element missing productId/quantity/priceCents is a 400, not a silent 0.
    o.SerializerOptions.RespectRequiredConstructorParameters = true;
});
builder.Services.AddOutputCache();
builder.Services.AddProblemDetails();

// Hash once so the per-request compare is fixed-length and constant-time. Empty or unset = not configured.
var adminKey = builder.Configuration["ADMIN_API_KEY"];
byte[]? adminKeyHash = string.IsNullOrWhiteSpace(adminKey) ? null : SHA256.HashData(Encoding.UTF8.GetBytes(adminKey));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseOutputCache();

// ponytail: migrate + seed in-process is fine for a single replica; move to a migration job if the API ever scales out.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    // Migrate() opens its first connection outside EnableRetryOnFailure; run it inside the strategy so the
    // first-boot window (pg_isready up, database not yet accepting) is retried, then fails after ~6 attempts.
    db.Database.CreateExecutionStrategy().Execute(() => db.Database.Migrate());
    Seed.Run(db);
}

app.MapGet("/health", () => "ok");

app.MapGet("/api/machines", (AppDb db) =>
        db.Machines
            .Where(m => m.IsActive)
            .OrderBy(m => m.Name)
            .Select(m => new MachineDto(m.Id, m.Slug, m.Name, m.Street, m.PostalCode, m.City, m.Lat, m.Lng, m.GoogleMapsUrl))
            .ToListAsync())
    .CacheOutput(p => p.Expire(TimeSpan.FromSeconds(60)));

app.MapGet("/api/machines/{id:int}/inventory", async (int id, AppDb db) =>
{
    if (!await db.Machines.AnyAsync(m => m.Id == id && m.IsActive))
        return Results.Problem(statusCode: StatusCodes.Status404NotFound);

    var rows = await db.MachineInventory
        .Where(i => i.MachineId == id)
        .OrderBy(i => i.Product.Name)
        .Select(i => new { i.ProductId, i.Product.Name, i.Product.Category, i.Quantity, i.PriceCents, i.UpdatedAt })
        .ToListAsync();

    // Category is stored as a string, so enum order (Vape, Drink, Snack) is applied here; OrderBy is stable, keeping name order.
    var items = rows
        .OrderBy(r => r.Category)
        .Select(r => new InventoryItemDto(r.ProductId, r.Name, r.Category, r.Quantity, r.PriceCents))
        .ToList();
    DateTime? updatedAt = rows.Count == 0 ? null : rows.Max(r => r.UpdatedAt);
    return Results.Ok(new InventoryDto(id, updatedAt, items));
});

// Trust boundary (CLAUDE.md §5.2). Order: 503 key not configured → 401 key → 404 machine → 400 body.
// The body is read only after authentication, so unauthenticated callers never reach the JSON parser.
// ponytail: no rate limit — the key must be long and random; add AddRateLimiter on this route if it is ever brute-forced.
app.MapPut("/api/machines/{id:int}/inventory", async (int id, HttpRequest request, AppDb db) =>
{
    if (adminKeyHash is null)
        return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, detail: "Inventory updates are not configured.");

    var provided = request.Headers["X-Api-Key"];
    if (provided.Count != 1 || !CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(provided.ToString())), adminKeyHash))
        return Results.Problem(statusCode: StatusCodes.Status401Unauthorized);

    if (!await db.Machines.AnyAsync(m => m.Id == id))
        return Results.Problem(statusCode: StatusCodes.Status404NotFound);

    InventoryWrite?[]? body;
    try
    {
        body = await request.ReadFromJsonAsync<InventoryWrite?[]>();
    }
    catch (Exception e) when (e is JsonException or InvalidOperationException or BadHttpRequestException)
    {
        // JsonException: malformed/missing fields; InvalidOperationException: wrong content type; BadHttpRequestException: empty/oversized body.
        return Results.Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Body must be a JSON array of { productId, quantity, priceCents }.");
    }
    if (body is null)
        return Results.Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Body must be a JSON array; send [] to clear the stock.");

    var errors = new Dictionary<string, string[]>();
    var seen = new HashSet<int>();
    for (var i = 0; i < body.Length; i++)
    {
        var w = body[i];
        if (w is null) { errors[$"[{i}]"] = ["Element must not be null."]; continue; }
        if (!seen.Add(w.ProductId)) errors[$"[{i}].productId"] = [$"Duplicate productId {w.ProductId}."];
        if (w.Quantity < 0) errors[$"[{i}].quantity"] = ["quantity must be >= 0."];
        if (w.PriceCents < 0) errors[$"[{i}].priceCents"] = ["priceCents must be >= 0."];
    }
    var ids = seen.ToArray();
    var known = await db.Products.Where(p => ids.Contains(p.Id)).Select(p => p.Id).ToListAsync();
    for (var i = 0; i < body.Length; i++)
        if (body[i] is { } w && !known.Contains(w.ProductId))
            errors[$"[{i}].productId"] = [$"Unknown productId {w.ProductId}."];
    if (errors.Count > 0)
        return Results.ValidationProblem(errors);

    // Replace = update rows that stay, add new ones, remove the rest; one SaveChanges is one transaction.
    var now = DateTime.UtcNow;
    var existing = await db.MachineInventory.Where(i => i.MachineId == id).ToDictionaryAsync(i => i.ProductId);
    foreach (var w in body)
    {
        if (existing.Remove(w!.ProductId, out var row))
        {
            row.Quantity = w.Quantity;
            row.PriceCents = w.PriceCents;
            row.UpdatedAt = now;
        }
        else
        {
            db.MachineInventory.Add(new MachineInventory
                { MachineId = id, ProductId = w.ProductId, Quantity = w.Quantity, PriceCents = w.PriceCents, UpdatedAt = now });
        }
    }
    db.MachineInventory.RemoveRange(existing.Values);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run();

// docs/PLAN.md §5 — wire names are camelCase (System.Text.Json web defaults), enums as strings.
record MachineDto(int Id, string Slug, string Name, string Street, string PostalCode, string City, double Lat, double Lng, string? GoogleMapsUrl);
record InventoryItemDto(int ProductId, string Name, Category Category, int Quantity, int PriceCents);
record InventoryDto(int MachineId, DateTime? UpdatedAt, List<InventoryItemDto> Items);
record InventoryWrite(int ProductId, int Quantity, int PriceCents);
