using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace SmokeMkk.Api;

// Vendon's `GET machine/{id}/products` — only the fields docs/PLAN.md §4.2 reads; everything else is ignored.
record VendonProducts([property: JsonPropertyName("result")] List<VendonProduct>? Result);
record VendonProduct(
    [property: JsonPropertyName("stock_id")] int StockId,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("amount")] int Amount,
    [property: JsonPropertyName("selections")] List<VendonSelection>? Selections);
record VendonSelection([property: JsonPropertyName("price")] decimal? Price);   // EUR: 10 = 10,00 €, 2.5 = 2,50 €

// docs/PLAN.md §4.2 "Vendon poll" (B8). One loop for the whole fleet: every `interval`, fetch each active machine's stock
// list from Vendon sequentially and replace its entry in `stock`; the inventory route answers from that dictionary.
// A failed fetch keeps the previous entry (stale beats empty); a failed pass never stops the loop.
// The key lives only in the named client's Authorization header (Program.cs): never logged, never in a URL.
// ponytail: 39 calls per pass around the clock; turn VENDON_POLL_SECONDS up if Vendon's quota complains, or fall back to
// the B7 on-demand proxy (git history, commit "B7").
class VendonPoller(IServiceScopeFactory scopes, IHttpClientFactory http, ILogger<VendonPoller> log, TimeSpan interval,
    ConcurrentDictionary<int, InventoryDto> stock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.LogInformation("Vendon poller started, interval {Interval} s.", interval.TotalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PassAsync(stoppingToken);
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)   // e.g. the database is down: log, wait, try again
            {
                log.LogWarning("Vendon pass failed: {Error}.", e.GetType().Name);
                try { await Task.Delay(interval, stoppingToken); } catch (OperationCanceledException) { return; }
            }
        }
    }

    async Task PassAsync(CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        var machines = await ActiveMachinesAsync(ct);
        var client = http.CreateClient("vendon");
        var fetched = 0;
        foreach (var (id, vendonId) in machines)
        {
            var items = await FetchAsync(client, id, vendonId, ct);
            if (items is null) continue;
            stock[id] = new InventoryDto(id, items.Count == 0 ? null : DateTime.UtcNow, items);
            fetched++;
        }
        log.LogInformation("Vendon pass done: {Fetched}/{Machines} machines in {Elapsed:F1} s.", fetched, machines.Count, watch.Elapsed.TotalSeconds);
    }

    async Task<List<(int Id, int VendonId)>> ActiveMachinesAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var rows = await db.Machines.Where(m => m.IsActive).OrderBy(m => m.Id).Select(m => new { m.Id, m.VendonId }).ToListAsync(ct);
        return rows.Select(m => (m.Id, m.VendonId)).ToList();
    }

    // null = this fetch failed (already logged); the caller keeps the previous entry.
    async Task<List<InventoryItemDto>?> FetchAsync(HttpClient client, int id, int vendonId, CancellationToken ct)
    {
        try
        {
            using var response = await client.GetAsync($"machine/{vendonId}/products", ct);
            if (!response.IsSuccessStatusCode)
            {
                log.LogWarning("Vendon answered {StatusCode} for machine {MachineId}.", (int)response.StatusCode, id);
                return null;
            }
            var products = (await response.Content.ReadFromJsonAsync<VendonProducts>(ct))?.Result;
            if (products is null)
            {
                log.LogWarning("Vendon answered without a result list for machine {MachineId}.", id);
                return null;
            }
            return Vendon.Map(products);
        }
        catch (Exception e) when ((e is HttpRequestException or TaskCanceledException or JsonException) && !ct.IsCancellationRequested)
        {
            log.LogWarning("Vendon request failed for machine {MachineId}: {Error}.", id, e.GetType().Name);
            return null;
        }
    }
}

static class Vendon
{
    // docs/PLAN.md §4.2 "Mapping of result[]". Vendon lists one product under several stock ids and slots on one machine,
    // so rows are grouped by the cleaned name (ordinal, ignore case): quantity is the sum, price the lowest, productId the
    // smallest stock_id. A row without a selection price is ignored, as is a bookkeeping row (HiddenPrefixes, B10);
    // a name with no remaining row is skipped. Sorted by category (enum order), then name.
    public static List<InventoryItemDto> Map(List<VendonProduct> products) =>
        products
            .Where(p => p.Type == "PRODUCT" && p.Name is not null)
            .Select(p => (Name: CleanName(p.Name!), p.StockId, p.Amount,
                Prices: (p.Selections ?? []).Select(s => s.Price).OfType<decimal>().ToList()))
            .Where(p => p.Prices.Count > 0 && !HiddenPrefixes.Any(h => p.Name.StartsWith(h, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new InventoryItemDto(g.Min(p => p.StockId), g.Key, Categorize(g.Key),
                Math.Max(0, g.Sum(p => p.Amount)), Math.Max(0, (int)Math.Round(g.SelectMany(p => p.Prices).Min() * 100))))
            .OrderBy(i => i.Category)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    // Vendon name → display name: leading "SM:" and surrounding whitespace removed, inner whitespace runs collapsed.
    public static string CleanName(string raw)
    {
        var n = raw.Trim();
        if (n.StartsWith("SM:", StringComparison.OrdinalIgnoreCase)) n = n[3..];
        return string.Join(' ', n.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    // docs/PLAN.md §4.2 "Product search" (B9): accent- and case-insensitive key, the same rule the frontend uses for places.
    public static string Fold(string s) =>
        string.Concat(s.Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
            .ToLowerInvariant().Trim();

    // docs/PLAN.md §4.2 "Category keywords": first list with a substring hit on the lower-cased name wins; none → Other.
    public static Category Categorize(string name)
    {
        var n = name.ToLowerInvariant();
        foreach (var (category, keywords) in Keywords)
            if (keywords.Any(n.Contains)) return category;
        return Category.Other;
    }

    // ponytail: the rule is a substring list, not a brand database — it will misfile a few of the 2779 catalogue items
    // (`Smash - Schokolade` is a snack, `Gizeh - Hemp Grinder` an accessory, fine; an unknown brand lands in `Sonstiges`,
    // visibly). Upgrade path: the owner tags products in Vendon Cloud with the six category names, and the mapping reads
    // `stock.tags` instead — one function. A keyword change is a PM edit in docs/PLAN.md §4.2 first; this is a verbatim copy.
    // docs/PLAN.md §4.2 hide rule (B10): Vendon bookkeeping entries, not products (`Divers - Abverkauf Vapes mwst 19%`,
    // `Neues Produkt Us Snacks - Spirale Testen`). A new prefix is a PM edit in §4.2 first.
    static readonly string[] HiddenPrefixes = ["Divers", "Neues Produkt"];

    static readonly (Category, string[])[] Keywords =
    [
        (Category.Vape, ["elf", "pod", "liquid", "vape", "vozol", "crystal", "hqd", "lost mar", "flerbar", "innocigs", "k#rwa", "kurwa", "puff", "e-shisha", "ivg", "la fume", "device", "nikotinfrei", "mg/ml", "ske ", "187 stra"]),
        (Category.Accessory, ["gizeh", "filter", "papes", "paper", "blättchen", "tips", "feuerzeug", "clipper", "grinder", "kohle", "cocos", "phunnel", "mundstück", "schlauch", "kohlenzange", "ocb", "raw ", "actitube", "hemp", "jaysafe", "cones", "aufsatz", "steinkopf", "aluminium", "kondom", "billy boy", "durex", "gaskartusche", "lachgas", "exotic whip", "luftballon", "hygiene", "lip gloss", "pokemon", "booster", "cards", "bazooka", "pulver", "squeeze"]),
        (Category.Tobacco, ["tobacco", "tabak", "pablo", "siberia", "snus", "pouches", "zigarett", "velo", "dry base", "hookain", "shisha", "nameless", "hasso", "holster", "almassiva", "argileh", "banger", "adalya", "aino", "al-waha", "7 days", "savu", "shades", "loyal", "moe's", "nakhla", "o's", "o`s", "true passion", "fadi", "element", "electro smog", "blackburn", "dschinni", "aqma", "babos", "maridan", "legacy", "lenon", "zomo", "fatality", "feders", "gun shot", "hard-korn", "havanna", "extreme", "xschischa", "zed ", "hookah", "da vinci", "fog your law", "aqua mentha"]),
        (Category.Drink, ["cola", "fanta", "sprite", "spezi", "red bull", "monster", "energy", "energie", "wasser", "water", "tea", "tee", "eiskaffe", "coffee", "kaffee", "saft", "juice", "drink", "getränk", "0,33", "0,5", "0.33", "0,25", "ml)", "ml ", "dose", "bier", "heineken", "krombacher", "corona", "desperados", "astra", "schlapp", "mooser", "pepsi", "7up", "mezzo mix", "mountain dew", "dr pepper", "powerade", "gatored", "capri", "arizona", "snapple", "ramune", "lipton", "moloko", "28 black", "silberpfeil", "rockstar", "effect", "smirnoff", "gordon", "gin ", "vodka", "morgan", "pitu", "gorbatschow", "msk", "jim beam", "calypso", "kool aid", "hawaiin punch", "big red", "cream soda", "aloe vera", "durstlöscher", "schwip schwap", "starbucks", "mr. brown", "scooper", "hot blood", "dirttea", "prime", "alkohol", "bembel", "schorle", "apfelwein"]),
        (Category.Snack, ["chips", "haribo", "kitkat", "kit kat", "twix", "mars", "snickers", "bueno", "kinder", "milka", "oreo", "schoko", "riegel", "keks", "cookie", "candy", "gummy", "gum", "airwaves", "hubba", "skittles", "skittels", "sour patch", "warheads", "takis", "cheetos", "chipseetos", "pringles", "pringels", "nic nac", "nic nak", "erdn", "peanut", "nuts", "m&m", "reese", "hostess", "bauli", "croissant", "cake", "kuchen", "donut", "pocky", "pepero", "ramen", "samyang", "jerky", "bifi", "salami", "pickle", "lutscher", "lollipop", "chup", "hitschies", "maoam", "jelly", "nerds", "toblerone", "maltesers", "mikado", "hanuta", "pick up", "mr.tom", "mr. beast", "ferrero", "nestle", "nestel", "cerali", "stroopwafel", "willis", "pretzel", "brezel", "flipz", "sticks", "spongebob", "popping", "airheads", "jolly rancher", "schokobon", "country", "snack", "sweets", "koala", "smash", "kinderini", "hot chip", "salt chip", "sunflower", "treats", "barbebells", "ibis", "space", "zinger", "lebensmittel", "klopfer", "herr's", "chio", "funny frisch", "flic", "pig", "brain blasterz", "striking", "fresh drink - jelly"]),
    ];
}
