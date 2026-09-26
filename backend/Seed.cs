using Microsoft.EntityFrameworkCore;

namespace SmokeMkk.Api;

// Seeds docs/PLAN.md §3 once. Runs only when Machines is empty, so operator edits are never overwritten.
// Not HasData on purpose: HasData would turn every seed change into a migration that rewrites live rows.
public static class Seed
{
    public static void Run(AppDb db)
    {
        if (db.Machines.Any()) return;

        // docs/PLAN.md §3: one row per site, with its machines (Vendon number, label; "" when alone). Id 7 (burgjoss) is not seeded.
        (Location Site, (int Id, string Label)[] Machines)[] sites =
        [
            (L(1, "fulda", "SMOKE Fulda", "Niesiger Str. 69", "36039", "Fulda", 50.56904, 9.66508), [(38, "")]),
            (L(2, "weisskirchen", "SMOKE Weisskirchen", "Hoher Nickel 14", "63110", "Rodgau", 50.05631, 8.88709), [(22, "")]),
            (L(3, "langenselbold", "SMOKE Langenselbold", "Steinweg 1A", "63505", "Langenselbold", 50.17617, 9.04094),
                [(23, "Blau"), (24, "Pink"), (25, "Gelb"), (26, "Grün")]),
            (L(4, "schluechtern", "SMOKE Schlüchtern", "Krämerstraße 14A", "36381", "Schlüchtern", 50.34755, 9.52710),
                [(28, "Pink"), (29, "Gelb"), (30, "Grün"), (31, "Blau"), (32, "Bunt")]),
            (L(5, "bad-orb", "SMOKE Bad Orb Jahnstraße", "Jahnstraße 33", "63619", "Bad Orb", 50.22030, 9.35482, "https://goo.gl/maps/xXyGLJiPCcnzB36h9"), [(7, "")]),
            (L(6, "jossgrund", "SMOKE Jossgrund", "Burgstraße 15", "63637", "Jossgrund (Burgjoß)", 50.20363, 9.48257, "https://goo.gl/maps/xX4skQTj9qrPxpgu8"),
                [(15, "Bunt"), (16, "Grün")]),
            (L(8, "waechtersbach", "SMOKE Wächtersbach", "Poststraße 33", "63607", "Wächtersbach", 50.25546, 9.29360, "https://goo.gl/maps/r33J2pLgynRgCQ8K8"),
                [(2, "Grün"), (3, "Bunt")]),
            (L(9, "rothenbergen", "SMOKE Rothenbergen", "Alte Dorfstraße 2A", "63584", "Gründau", 50.19871, 9.10919, "https://maps.app.goo.gl/4w2qC9V7qHrYzuxk9"), [(27, "")]),
            (L(10, "hesseldorf", "SMOKE Hesseldorf", "Brachttalstraße 18", "63607", "Wächtersbach", 50.27203, 9.30658, "https://goo.gl/maps/AxoLgpYdEnATwhj67"), [(1, "")]),
            (L(11, "hellstein", "SMOKE Hellstein", "Sandwerkstraße 2", "63636", "Brachttal", 50.32064, 9.29995, "https://maps.app.goo.gl/z3jF2pLDndePZfL87"), [(6, "")]),
            (L(12, "lauterbach", "SMOKE Lauterbach", "Marktplatz 7", "36341", "Lauterbach", 50.63638, 9.39617, "https://maps.app.goo.gl/9j1BeUM7qcCZ44b67"),
                [(34, "Gelb"), (35, "Pink"), (36, "Grün"), (37, "Blau")]),
            (L(13, "bad-orb-kanalstrasse", "SMOKE Bad Orb Kanalstraße", "Kanalstraße 37", "63619", "Bad Orb", 50.22502, 9.34829),
                [(9, "Blau"), (10, "Pink"), (11, "Grün")]),
            (L(14, "bad-orb-martinusstrasse", "SMOKE Bad Orb Martinusstraße", "Martinusstraße 14", "63619", "Bad Orb", 50.23148, 9.34326), [(8, "")]),
            (L(15, "bad-orb-frankfurter-strasse", "SMOKE Bad Orb Frankfurter Straße", "Frankfurter Straße 4", "63619", "Bad Orb", 50.22887, 9.34495), [(12, "")]),
            (L(16, "bad-orb-wuerzburger-strasse", "SMOKE Bad Orb Würzburger Straße", "Würzburger Straße 51", "63619", "Bad Orb", 50.22133, 9.35580), [(13, "")]),
            (L(17, "neuenschmidten", "SMOKE Neuenschmidten", "Birsteiner Straße 55", "63636", "Brachttal", 50.31426, 9.29194), [(5, "")]),
            (L(18, "spielberg", "SMOKE Spielberg", "Schulwaldstraße 2", "63636", "Brachttal", 50.30785, 9.26896), [(4, "")]),
            (L(19, "lettgenbrunn", "SMOKE Lettgenbrunn", "Hindenburgstraße 7", "63637", "Jossgrund", 50.17620, 9.39456),
                [(17, "Halfway House"), (18, "Driving Range")]),
            (L(20, "mernes", "SMOKE Mernes", "Orber Weg 35", "63628", "Bad Soden-Salmünster", 50.23579, 9.46929), [(14, "")]),
            (L(21, "neuberg", "SMOKE Neuberg", "Siedlung 2A", "63543", "Neuberg", 50.19158, 8.99060), [(21, "")]),
            (L(22, "steinau", "SMOKE Steinau", "Leipziger Straße 85", "36396", "Steinau an der Straße", 50.31905, 9.47210), [(33, "")]),
            (L(23, "kempfenbrunn", "SMOKE Kempfenbrunn", "Würzburger Straße 26", "63639", "Flörsbachtal", 50.11118, 9.44013), [(19, "")]),
            (L(24, "alsfeld", "SMOKE Alsfeld", "Enggasse 7", "36304", "Alsfeld", 50.75151, 9.27258), [(39, "")]),
            (L(25, "lohr", "SMOKE Lohr", "Willi-Bleicher-Straße 1", "97816", "Lohr a. Main", 50.00629, 9.57408), [(20, "")]),
        ];
        var machines = sites
            .SelectMany(s => s.Machines, (s, m) => new Machine { Id = m.Id, LocationId = s.Site.Id, Label = m.Label, IsActive = true })
            .ToArray();

        // Placeholder catalogue (TODO(owner): confirm or replace). Image files are supplied by the owner in frontend/public/products/. Price is per machine in MachineInventory.
        (Product Product, int PriceCents)[] catalogue =
        [
            (new() { Id = 1, Name = "Elf Bar 600 Blueberry Ice", Category = Category.Vape, ImageUrl = "/products/elfbar-600-blueberry-ice.webp" }, 1299),
            (new() { Id = 2, Name = "Elf Bar 600 Watermelon", Category = Category.Vape, ImageUrl = "/products/elfbar-600-watermelon.webp" }, 1199),
            (new() { Id = 3, Name = "Elf Bar 600 Cola", Category = Category.Vape, ImageUrl = "/products/elfbar-600-cola.webp" }, 899),
            (new() { Id = 4, Name = "Red Bull Energy Drink 250 ml", Category = Category.Drink, ImageUrl = "/products/red-bull-250.webp" }, 299),
            (new() { Id = 5, Name = "Coca-Cola Zero 330 ml", Category = Category.Drink, ImageUrl = "/products/coca-cola-zero-330.webp" }, 350),
            (new() { Id = 6, Name = "Vio Wasser still 500 ml", Category = Category.Drink, ImageUrl = "/products/vio-still-500.webp" }, 250),
            (new() { Id = 7, Name = "Snickers", Category = Category.Snack, ImageUrl = "/products/snickers.webp" }, 150),
            (new() { Id = 8, Name = "Haribo Goldbären 100 g", Category = Category.Snack, ImageUrl = "/products/haribo-goldbaeren-100.webp" }, 199),
            (new() { Id = 9, Name = "Pringles Paprika 40 g", Category = Category.Snack, ImageUrl = "/products/pringles-paprika-40.webp" }, 250),
            (new() { Id = 10, Name = "Marlboro Red 20 Stück", Category = Category.Tobacco, ImageUrl = "/products/marlboro-red-20.webp" }, 1100),
            (new() { Id = 11, Name = "Pueblo Classic Tabak 30 g", Category = Category.Tobacco, ImageUrl = "/products/pueblo-classic-30.webp" }, 995),
            (new() { Id = 12, Name = "OCB Slim Premium Papers", Category = Category.Accessory, ImageUrl = "/products/ocb-slim-premium.webp" }, 150),
            (new() { Id = 13, Name = "Clipper Feuerzeug", Category = Category.Accessory, ImageUrl = "/products/clipper-feuerzeug.webp" }, 250),
        ];

        // Rotated per machine: every machine shows all three stock states (>3, 1–3, 0).
        int[] quantities = [7, 5, 0, 12, 2, 8, 6, 0, 3, 9, 1, 4, 10];
        var now = DateTime.UtcNow;

        db.Locations.AddRange(sites.Select(s => s.Site));
        db.Machines.AddRange(machines);
        db.Products.AddRange(catalogue.Select(c => c.Product));
        db.MachineInventory.AddRange(
            from m in machines
            from c in catalogue.Select((c, i) => (c, i))
            select new MachineInventory
            {
                MachineId = m.Id,
                ProductId = c.c.Product.Id,
                Quantity = quantities[(c.i + m.Id) % quantities.Length],
                PriceCents = c.c.PriceCents,
                UpdatedAt = now,
            });
        db.SaveChanges();

        // Explicit ids bypass the identity sequences; move them past the seed so later inserts do not collide.
        db.Database.ExecuteSqlRaw("""SELECT setval(pg_get_serial_sequence('"Locations"', 'Id'), (SELECT MAX("Id") FROM "Locations"))""");
        db.Database.ExecuteSqlRaw("""SELECT setval(pg_get_serial_sequence('"Machines"', 'Id'), (SELECT MAX("Id") FROM "Machines"))""");
        db.Database.ExecuteSqlRaw("""SELECT setval(pg_get_serial_sequence('"Products"', 'Id'), (SELECT MAX("Id") FROM "Products"))""");
    }

    // #1–#4, #13–#25: Google search on the street address (docs/PLAN.md §3).
    static Location L(int id, string slug, string name, string street, string postalCode, string city,
        double lat, double lng) =>
        L(id, slug, name, street, postalCode, city, lat, lng,
            "https://www.google.com/maps/search/?api=1&query=" + Uri.EscapeDataString($"{street}, {postalCode} {city}"));

    // #5, #6, #8–#12: the original Linktree short link.
    static Location L(int id, string slug, string name, string street, string postalCode, string city,
        double lat, double lng, string url) => new()
    {
        Id = id, Slug = slug, Name = name, Street = street, PostalCode = postalCode, City = city,
        Lat = lat, Lng = lng, GoogleMapsUrl = url,
    };
}
