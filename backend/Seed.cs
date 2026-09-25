using Microsoft.EntityFrameworkCore;

namespace SmokeMkk.Api;

// Seeds docs/PLAN.md §3 once. Runs only when Machines is empty, so operator edits are never overwritten.
// Not HasData on purpose: HasData would turn every seed change into a migration that rewrites live rows.
public static class Seed
{
    public static void Run(AppDb db)
    {
        if (db.Machines.Any()) return;

        Machine[] machines =
        [
            M(1, "fulda", "SMOKE Fulda", "Niesiger Str. 69", "36039", "Fulda", 50.56899, 9.66503),
            M(2, "weisskirchen", "SMOKE Weisskirchen", "Hoher Nickel 14", "63110", "Rodgau", 50.05644, 8.88740),
            M(3, "langenselbold", "SMOKE Langenselbold", "Steinweg 1A", "63505", "Langenselbold", 50.17634, 9.04093),
            M(4, "schluechtern", "SMOKE Schlüchtern", "Krämerstraße 14", "36381", "Schlüchtern", 50.34747, 9.52716),
            M(5, "bad-orb", "SMOKE Bad Orb", "Jahnstraße 42", "63619", "Bad Orb", 50.22044, 9.35409, "https://goo.gl/maps/xXyGLJiPCcnzB36h9"),
            M(6, "jossgrund", "SMOKE Jossgrund", "Burgstraße 15", "63637", "Jossgrund (Burgjoß)", 50.20378, 9.48254, "https://goo.gl/maps/xX4skQTj9qrPxpgu8"),
            // Linktree link points at the Langenselbold pin; probably a duplicate of #6. Hidden until the owner confirms.
            M(7, "burgjoss", "SMOKE Burgjoss", "", "63637", "Jossgrund", 50.20380, 9.48049, url: null, active: false),
            M(8, "waechtersbach", "SMOKE Wächtersbach", "Poststraße 33", "63607", "Wächtersbach", 50.25550, 9.29381, "https://goo.gl/maps/r33J2pLgynRgCQ8K8"),
            M(9, "rothenbergen", "SMOKE Rothenbergen", "Alte Dorfstraße 2A", "63584", "Gründau", 50.19879, 9.10918, "https://maps.app.goo.gl/4w2qC9V7qHrYzuxk9"),
            M(10, "hesseldorf", "SMOKE Hesseldorf", "Brachttalstraße 18", "63607", "Wächtersbach", 50.27203, 9.30658, "https://goo.gl/maps/AxoLgpYdEnATwhj67"),
            M(11, "hellstein", "SMOKE Hellstein", "Sandwerkstraße 2", "63636", "Brachttal", 50.32034, 9.30018, "https://maps.app.goo.gl/z3jF2pLDndePZfL87"),
            M(12, "lauterbach", "SMOKE Lauterbach", "Marktplatz", "36341", "Lauterbach", 50.63632, 9.39614, "https://maps.app.goo.gl/9j1BeUM7qcCZ44b67"),
        ];

        // Placeholder catalogue (TODO(owner): real catalogue via PUT). Price is per machine in MachineInventory.
        (Product Product, int PriceCents)[] catalogue =
        [
            (new() { Id = 1, Name = "Einweg-Vape Blueberry Ice (600 Züge)", Category = Category.Vape }, 1299),
            (new() { Id = 2, Name = "Einweg-Vape Watermelon (600 Züge)", Category = Category.Vape }, 1199),
            (new() { Id = 3, Name = "Nikotin-Pouches Mint", Category = Category.Vape }, 899),
            (new() { Id = 4, Name = "Energy Drink 250 ml", Category = Category.Drink }, 299),
            (new() { Id = 5, Name = "Cola 330 ml", Category = Category.Drink }, 350),
            (new() { Id = 6, Name = "Wasser still 500 ml", Category = Category.Drink }, 250),
            (new() { Id = 7, Name = "Schokoriegel", Category = Category.Snack }, 150),
            (new() { Id = 8, Name = "Fruchtgummi 100 g", Category = Category.Snack }, 199),
            (new() { Id = 9, Name = "Chips Paprika 50 g", Category = Category.Snack }, 250),
        ];

        // Rotated per machine: every machine shows all three stock states (>3, 1–3, 0).
        int[] quantities = [7, 5, 0, 12, 2, 8, 6, 0, 3];
        var now = DateTime.UtcNow;

        db.Machines.AddRange(machines);
        db.Products.AddRange(catalogue.Select(c => c.Product));
        db.MachineInventory.AddRange(
            from m in machines.Where(m => m.IsActive)
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
        db.Database.ExecuteSqlRaw("""SELECT setval(pg_get_serial_sequence('"Machines"', 'Id'), (SELECT MAX("Id") FROM "Machines"))""");
        db.Database.ExecuteSqlRaw("""SELECT setval(pg_get_serial_sequence('"Products"', 'Id'), (SELECT MAX("Id") FROM "Products"))""");
    }

    // #1–#4: Google search on the street address (docs/PLAN.md §3).
    static Machine M(int id, string slug, string name, string street, string postalCode, string city,
        double lat, double lng) =>
        M(id, slug, name, street, postalCode, city, lat, lng,
            "https://www.google.com/maps/search/?api=1&query=" + Uri.EscapeDataString($"{street}, {postalCode} {city}"));

    // #5–#12: the original Linktree short link, or null when it is known to be wrong (#7).
    static Machine M(int id, string slug, string name, string street, string postalCode, string city,
        double lat, double lng, string? url, bool active = true) => new()
    {
        Id = id, Slug = slug, Name = name, Street = street, PostalCode = postalCode, City = city,
        Lat = lat, Lng = lng, IsActive = active, GoogleMapsUrl = url,
    };
}
