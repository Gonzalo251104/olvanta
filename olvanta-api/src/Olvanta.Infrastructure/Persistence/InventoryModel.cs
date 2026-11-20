using Microsoft.EntityFrameworkCore;
using Olvanta.Domain;

namespace Olvanta.Infrastructure.Persistence;

internal static class InventoryModel
{
    internal static void Configure(ModelBuilder model)
    {
        var warehouse = model.Entity<Warehouse>();
        warehouse.ToTable("Warehouses", "inventory");
        warehouse.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        warehouse.Property(x => x.Name).HasMaxLength(100);
        warehouse.HasIndex(x => new { x.BranchId, x.Name }).IsUnique();

        var purchase = model.Entity<Purchase>();
        purchase.ToTable("Purchases", "inventory");
        purchase.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        purchase.HasOne<MasterRecord>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        purchase.Property(x => x.Currency).HasMaxLength(3);
        purchase.Property(x => x.Reference).HasMaxLength(80);
        purchase.HasIndex(x => new { x.BranchId, x.SupplierId, x.Reference }).IsUnique();

        var line = model.Entity<PurchaseLine>();
        line.ToTable("PurchaseLines", "inventory");
        line.HasOne<MasterRecord>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        line.Property(x => x.Quantity).HasPrecision(18, 6);
        line.Property(x => x.Received).HasPrecision(18, 6);
        line.Property(x => x.UnitCost).HasPrecision(18, 6);

        var minimum = model.Entity<StockMinimum>();
        minimum.ToTable("Minimums", "inventory");
        minimum.HasKey(x => new { x.WarehouseId, x.ProductId });
        minimum.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        minimum.HasOne<MasterRecord>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        minimum.Property(x => x.Quantity).HasPrecision(18, 6);

        var move = model.Entity<StockMovement>();
        move.ToTable("Movements", "inventory");
        move.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        move.HasOne<MasterRecord>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        move.HasOne<PurchaseLine>().WithMany().HasForeignKey(x => x.PurchaseLineId).OnDelete(DeleteBehavior.Restrict);
        move.HasOne<StockMovement>().WithMany().HasForeignKey(x => x.RelatedId).OnDelete(DeleteBehavior.Restrict);
        move.Property(x => x.Kind).HasMaxLength(24);
        move.Property(x => x.Actor).HasMaxLength(256);
        move.Property(x => x.RequestHash).HasMaxLength(64);
        move.Property(x => x.Reason).HasMaxLength(240);
        move.Property(x => x.Lot).HasMaxLength(80);
        move.Property(x => x.Quantity).HasPrecision(18, 6);
        move.Property(x => x.Reserved).HasPrecision(18, 6);
        move.Property(x => x.Value).HasPrecision(28, 6);
        move.HasIndex(x => new { x.BranchId, x.OperationId, x.WarehouseId }).IsUnique();
        move.HasIndex(x => new { x.BranchId, x.ProductId, x.WarehouseId, x.Lot });
    }
}
