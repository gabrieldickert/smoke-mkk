using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmokeMkk.Api;

public enum Category { Vape, Drink, Snack }

public class Machine
{
    public int Id { get; set; }
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public required string Street { get; set; }
    public required string PostalCode { get; set; }
    public required string City { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
    public bool IsActive { get; set; }
    public string? GoogleMapsUrl { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public Category Category { get; set; }
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
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<MachineInventory> MachineInventory => Set<MachineInventory>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Machine>().HasIndex(m => m.Slug).IsUnique();

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
