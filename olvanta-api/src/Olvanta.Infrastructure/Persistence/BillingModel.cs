using Microsoft.EntityFrameworkCore;
using Olvanta.Domain;

namespace Olvanta.Infrastructure.Persistence;

internal static class BillingModel
{
    internal static void Configure(ModelBuilder model)
    {
        var doc = model.Entity<FiscalDocument>();
        doc.ToTable("Documents", "billing");
        doc.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        doc.HasOne<Sale>().WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        doc.HasOne<FiscalDocument>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        doc.Property(x => x.Type).HasMaxLength(2);
        doc.Property(x => x.Series).HasMaxLength(16);
        doc.Property(x => x.Mode).HasMaxLength(16);
        doc.Property(x => x.SourceKey).HasMaxLength(140);
        doc.Property(x => x.RequestHash).HasMaxLength(64);
        doc.Property(x => x.Status).HasMaxLength(24);
        doc.HasIndex(x => new { x.Mode, x.Type, x.Series, x.Number }).IsUnique();
        doc.HasIndex(x => new { x.Mode, x.SourceKey }).HasFilter("[Active] = 1").IsUnique();
        doc.HasIndex(x => new { x.BranchId, x.RequestId }).IsUnique();
        doc.HasIndex(x => new { x.Mode, x.NextQuery }).HasFilter("[NextQuery] IS NOT NULL");
        var attempt = model.Entity<FiscalAttempt>();
        attempt.ToTable("Attempts", "billing");
        attempt.HasOne<FiscalDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        var receipt = model.Entity<SimulationReceipt>();
        receipt.ToTable("SimulationReceipts", "billing");
        receipt.HasKey(x => x.DocumentId);
        receipt.Property(x => x.DocumentId).ValueGeneratedNever();
        receipt.HasOne<FiscalDocument>().WithOne().HasForeignKey<SimulationReceipt>(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
