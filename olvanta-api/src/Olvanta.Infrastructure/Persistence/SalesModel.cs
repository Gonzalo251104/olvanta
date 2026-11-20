using Microsoft.EntityFrameworkCore;
using Olvanta.Domain;

namespace Olvanta.Infrastructure.Persistence;

internal static class SalesModel
{
    internal static void Configure(ModelBuilder model)
    {
        var sale = model.Entity<Sale>();
        sale.ToTable("Sales", "commercial");
        sale.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        sale.HasOne<MasterRecord>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        sale.Property(x => x.Currency).HasMaxLength(3);
        sale.Property(x => x.Terms).HasMaxLength(12);
        sale.Property(x => x.Stage).HasMaxLength(16);
        sale.Property(x => x.Customer).HasMaxLength(240);
        var line = model.Entity<SaleLine>();
        line.ToTable("SaleLines", "commercial");
        line.HasOne<MasterRecord>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        line.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        line.Property(x => x.Quantity).HasPrecision(18, 6);
        line.Property(x => x.Price).HasPrecision(16, 2);
        line.Property(x => x.TaxRate).HasPrecision(5, 2);
        line.Property(x => x.Discount).HasPrecision(5, 2);
        line.Property(x => x.Total).HasPrecision(16, 2);
        line.Property(x => x.Tax).HasPrecision(16, 2);
        line.Property(x => x.Lot).HasMaxLength(80);
        var session = model.Entity<CashSession>();
        session.ToTable("CashSessions", "commercial");
        session.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        session.HasIndex(x => x.BranchId).HasFilter("[Closed] IS NULL").IsUnique();
        session.Property(x => x.Opening).HasPrecision(16, 2);
        session.Property(x => x.Counted).HasPrecision(16, 2);
        session.Property(x => x.Currency).HasMaxLength(3);
        var journal = model.Entity<SalesEvent>();
        journal.ToTable("Events", "commercial");
        journal.HasIndex(x => new { x.BranchId, x.RequestId }).IsUnique();
        journal.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        journal.HasOne<Sale>().WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        journal.HasOne<SaleLine>().WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Restrict);
        journal.HasOne<CashSession>().WithMany().HasForeignKey(x => x.CashSessionId).OnDelete(DeleteBehavior.Restrict);
        journal.HasIndex(x => x.CashSessionId).IncludeProperties(x => x.CashDelta);
        journal.HasOne<SalesEvent>().WithMany().HasForeignKey(x => x.ReturnId).OnDelete(DeleteBehavior.Restrict);
        journal.Property(x => x.Amount).HasPrecision(16, 2);
        journal.Property(x => x.Quantity).HasPrecision(18, 6);
        journal.Property(x => x.CashDelta).HasPrecision(16, 2);
        journal.Property(x => x.RequestHash).HasMaxLength(64);
        journal.Property(x => x.Kind).HasMaxLength(20);
        journal.Property(x => x.Method).HasMaxLength(16);
        journal.Property(x => x.Note).HasMaxLength(240);
        model.Entity<StockMovement>().HasOne<SaleLine>().WithMany().HasForeignKey(x => x.SaleLineId).OnDelete(DeleteBehavior.Restrict);
    }
}
