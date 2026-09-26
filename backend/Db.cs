using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmokeMkk.Api;

// Declaration order is the display and sort order of inventory items (docs/PLAN.md §3, §5).
public enum Category { Vape, Tobacco, Accessory, Drink, Snack }

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

// One vending machine with its own stock; Id is the Vendon device number.
public class Machine
{
    public int Id { get; set; }
    public int LocationId { get; set; }
    public string Label { get; set; } = "";   // "" when it is the only machine at its location
    public bool IsActive { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public Category Category { get; set; }
    public string? ImageUrl { get; set; }   // site-relative, e.g. "/products/red-bull-250.webp" (docs/PLAN.md §3)
}

public class MachineInventory
{
    public int MachineId { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public int PriceCents { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<MachineInventory> MachineInventory => Set<MachineInventory>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Location>().HasIndex(l => l.Slug).IsUnique();

        b.Entity<Product>().Property(p => p.Category).HasConversion<string>();

        b.Entity<MachineInventory>(e =>
        {
            e.HasKey(i => new { i.MachineId, i.ProductId });
            e.HasOne<Machine>().WithMany().HasForeignKey(i => i.MachineId);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_MachineInventory_Quantity", "\"Quantity\" >= 0");
                t.HasCheckConstraint("CK_MachineInventory_PriceCents", "\"PriceCents\" >= 0");
            });
        });
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
