using Concurrency_Safe_Inventory_Reservation.Inventory.Models;
using Microsoft.EntityFrameworkCore;

namespace Concurrency_Safe_Inventory_Reservation.Inventory.Data;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
    {
    }

    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InventoryItem>(b =>
        {
     b.HasKey(i => i.Sku);
     b.Property(i => i.Sku).HasMaxLength(64);
     b.Property(i => i.AvailableQuantity).IsRequired();

     // Provider agnostic optimistic concurrency token.
     // On SQL Server you would replace this with: b.Property(i => i.RowVersion).IsRowVersion();
     b.Property(i => i.RowVersion)
      .IsConcurrencyToken()
      .HasMaxLength(16);
        });

        modelBuilder.Entity<Reservation>(b =>
        {
     b.HasKey(r => r.Id);
     b.Property(r => r.IdempotencyKey).HasMaxLength(128).IsRequired();
            b.Property(r => r.Sku).HasMaxLength(64).IsRequired();

     // The database is the final authority for idempotency.
     b.HasIndex(r => r.IdempotencyKey).IsUnique();
        });
    }
}
