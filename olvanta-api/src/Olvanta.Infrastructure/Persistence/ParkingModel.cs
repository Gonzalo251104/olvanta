using Microsoft.EntityFrameworkCore;
using Olvanta.Domain;

namespace Olvanta.Infrastructure.Persistence;

internal static class ParkingModel
{
    internal static void Configure(ModelBuilder model)
    {
        var site = model.Entity<ParkingSite>();
        site.ToTable("Sites", "parking");
        site.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        site.Property(x => x.Name).HasMaxLength(100);
        site.Property(x => x.Currency).HasMaxLength(3);
        site.HasIndex(x => new { x.BranchId, x.Name }).IsUnique();

        var zone = model.Entity<ParkingZone>();
        zone.ToTable("Zones", "parking");
        zone.HasOne<ParkingSite>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        zone.Property(x => x.Name).HasMaxLength(100);
        zone.HasIndex(x => new { x.SiteId, x.Name }).IsUnique();

        var rate = model.Entity<ParkingRate>();
        rate.ToTable("Rates", "parking");
        rate.HasOne<ParkingSite>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        rate.HasOne<MasterRecord>().WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Restrict);
        rate.Property(x => x.BlockPrice).HasPrecision(16, 2);
        rate.Property(x => x.TaxRate).HasPrecision(5, 2);

        var pass = model.Entity<ParkingPass>();
        pass.ToTable("Passes", "parking");
        pass.HasOne<ParkingSite>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        pass.HasOne(x => x.Payment).WithOne().HasForeignKey<ParkingPass>(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        pass.Property(x => x.Plate).HasMaxLength(12);
        pass.Property(x => x.RequestHash).HasMaxLength(64);
        pass.HasIndex(x => new { x.SiteId, x.RequestId }).IsUnique();
        pass.HasIndex(x => new { x.SiteId, x.Plate, x.From });

        var stay = model.Entity<ParkingStay>();
        stay.ToTable("Stays", "parking");
        stay.HasOne<ParkingSite>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        stay.HasOne<ParkingZone>().WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
        stay.HasOne<ParkingRate>().WithMany().HasForeignKey(x => x.RateId).OnDelete(DeleteBehavior.Restrict);
        stay.HasOne<ParkingPass>().WithMany().HasForeignKey(x => x.PassId).OnDelete(DeleteBehavior.Restrict);
        stay.HasOne(x => x.Payment).WithOne().HasForeignKey<ParkingStay>(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        stay.Property(x => x.Plate).HasMaxLength(12);
        stay.Property(x => x.ExitHash).HasMaxLength(64);
        stay.HasIndex(x => new { x.SiteId, x.Plate }).IsUnique().HasFilter("[Exited] IS NULL");
        stay.HasIndex(x => new { x.SiteId, x.EntryKey }).IsUnique();
        stay.HasIndex(x => new { x.SiteId, x.ExitKey }).IsUnique().HasFilter("[ExitKey] IS NOT NULL");
        stay.HasIndex(x => x.ZoneId).HasFilter("[Exited] IS NULL");
    }
}
