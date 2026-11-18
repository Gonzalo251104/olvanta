using Microsoft.EntityFrameworkCore;
using Olvanta.Domain;

namespace Olvanta.Infrastructure.Persistence;

internal static class RentalModel
{
    internal static void Configure(ModelBuilder model)
    {
        var unit = model.Entity<CommercialUnit>();
        unit.ToTable("Units", "rentals");
        unit.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        unit.Property(x => x.Code).HasMaxLength(30);
        unit.HasIndex(x => new { x.BranchId, x.Code }).IsUnique();
        var lease = model.Entity<LeaseContract>();
        lease.ToTable("Contracts", "rentals");
        lease.HasOne<CommercialUnit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        lease.HasIndex(x => new { x.UnitId, x.Start, x.End });
        lease.HasOne<LeaseContract>().WithOne().HasForeignKey<LeaseContract>(x => x.PreviousId).OnDelete(DeleteBehavior.Restrict);
        lease.HasOne<MasterRecord>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        lease.HasOne<MasterRecord>().WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Restrict);
        lease.Property(x => x.Deposit).HasPrecision(16, 2);
        lease.Property(x => x.MonthlyRent).HasPrecision(16, 2);
        lease.Property(x => x.TaxRate).HasPrecision(5, 2);
        var charge = model.Entity<RentCharge>();
        charge.ToTable("Charges", "rentals");
        charge.HasOne<LeaseContract>().WithMany().HasForeignKey(x => x.LeaseId).OnDelete(DeleteBehavior.Restrict);
        charge.HasOne(x => x.Sale).WithOne().HasForeignKey<RentCharge>(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        charge.Property(x => x.Amount).HasPrecision(16, 2);
        charge.Property(x => x.Kind).HasMaxLength(12);
        charge.Property(x => x.Reference).HasMaxLength(64);
        charge.HasIndex(x => new { x.LeaseId, x.Kind, x.Reference }).IsUnique();
        model.Entity<SalesEvent>().HasOne<LeaseContract>().WithMany().HasForeignKey(x => x.DepositLeaseId).OnDelete(DeleteBehavior.Restrict);
    }
}
