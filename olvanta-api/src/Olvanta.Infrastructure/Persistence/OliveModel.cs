using Microsoft.EntityFrameworkCore;
using Olvanta.Domain;

namespace Olvanta.Infrastructure.Persistence;

internal static class OliveModel
{
    internal static void Configure(ModelBuilder model)
    {
        var lot = model.Entity<OliveLot>();
        lot.ToTable("Lots", "olives");
        lot.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        lot.HasOne<MasterRecord>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        lot.HasOne<OliveLot>().WithMany().HasForeignKey(x => x.RootId).OnDelete(DeleteBehavior.Restrict);
        lot.HasOne<OliveProcess>().WithMany().HasForeignKey(x => x.ProcessId).OnDelete(DeleteBehavior.Restrict);
        lot.HasOne(x => x.Receipt).WithOne().HasForeignKey<OliveLot>(x => x.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        lot.Property(x => x.Code).HasMaxLength(80);
        lot.Property(x => x.KgPerUnit).HasPrecision(18, 6);
        lot.Property(x => x.Gross).HasPrecision(18, 6);
        lot.Property(x => x.Tare).HasPrecision(18, 6);
        lot.HasIndex(x => new { x.BranchId, x.ProductId, x.Code }).IsUnique();

        var process = model.Entity<OliveProcess>();
        process.ToTable("Repacking", "olives");
        process.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        process.HasOne<OliveLot>().WithMany().HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.Restrict);
        process.HasOne(x => x.Consume).WithOne().HasForeignKey<OliveProcess>(x => x.ConsumeId).OnDelete(DeleteBehavior.Restrict);
        process.Property(x => x.InputKg).HasPrecision(18, 6);
        process.Property(x => x.LossKg).HasPrecision(18, 6);
        process.Property(x => x.LossValue).HasPrecision(28, 6);
        process.HasIndex(x => new { x.BranchId, x.OperationId }).IsUnique();
    }
}
