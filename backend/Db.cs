using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmokeMkk.Api;

// Declaration order is the display and sort order of inventory items (docs/PLAN.md §4.2, §5). Used only in the DTO since B7;
// Other = no keyword matched. Stock itself is not stored: it comes live from Vendon per request.
public enum Category { Vape, Tobacco, Accessory, Drink, Snack, Other }

// One site = one map marker (docs/PLAN.md §3, §4.2). A site can hold several machines.
public class Location
{
    public int Id { get; set; }
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public required string Street { get; set; }
    public required string PostalCode { get; set; }
    public required string City { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string? GoogleMapsUrl { get; set; }
    public List<Machine> Machines { get; set; } = [];
}

// One vending machine; Id is the Vendon device number. Its stock is fetched live from Vendon by VendonId.
public class Machine
{
    public int Id { get; set; }
    public int LocationId { get; set; }
    public string Label { get; set; } = "";   // "" when it is the only machine at its location
    public bool IsActive { get; set; }
    public int VendonId { get; set; }   // the machine's id in the Vendon Cloud API (docs/PLAN.md §3)
    public string? PictureUrl { get; set; }   // absolute URL of a photo of the machine; null for most (docs/PLAN.md §3 "Machine photos")
}

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Machine> Machines => Set<Machine>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Location>().HasIndex(l => l.Slug).IsUnique();
        b.Entity<Machine>().HasIndex(m => m.VendonId).IsUnique();
    }
}

// Used only by `dotnet ef` at design time, so migrations can be generated without a running database.
public class AppDbDesignTimeFactory : IDesignTimeDbContextFactory<AppDb>
{
    public AppDb CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDb>()
            .UseNpgsql("Host=localhost;Database=smoke;Username=smoke")
            .Options);
}
