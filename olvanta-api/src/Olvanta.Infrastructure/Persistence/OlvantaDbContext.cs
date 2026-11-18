using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Olvanta.Domain;

namespace Olvanta.Infrastructure.Persistence;

public sealed class OlvantaDbContext(DbContextOptions<OlvantaDbContext> options) : IdentityDbContext<Operator>(options)
{
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Establishment> Establishments => Set<Establishment>();
    public DbSet<DocumentSeries> Series => Set<DocumentSeries>();
    public DbSet<BranchGrant> Grants => Set<BranchGrant>();
    public DbSet<MasterRecord> Masters => Set<MasterRecord>();
    public DbSet<AuditEntry> Audit => Set<AuditEntry>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseLine> PurchaseLines => Set<PurchaseLine>();
    public DbSet<StockMinimum> Minimums => Set<StockMinimum>();
    public DbSet<StockMovement> Movements => Set<StockMovement>();

    // Call inside a transaction. Stock writers in other branches can proceed independently.
    public Task<Branch?> LockBranchAsync(int branchId) => Branches
        .FromSqlInterpolated($"SELECT * FROM [organization].[Branches] WITH (XLOCK, ROWLOCK) WHERE Id = {branchId}")
        .SingleOrDefaultAsync();

    public Task<Branch?> ReadInventoryBranchAsync(int branchId) => Branches
        .FromSqlInterpolated($"SELECT * FROM [organization].[Branches] WITH (HOLDLOCK, ROWLOCK) WHERE Id = {branchId}")
        .AsNoTracking().SingleOrDefaultAsync();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.HasDefaultSchema("identity");
        InventoryModel.Configure(model);
        var branch = model.Entity<Branch>();
        branch.ToTable("Branches", "organization");
        branch.Property(x => x.Name).HasMaxLength(100);
        branch.Property(x => x.Currency).HasMaxLength(3).HasDefaultValue("PEN");
        branch.Property(x => x.TaxRate).HasPrecision(5, 2);
        model.Entity<Company>().ToTable("Company", "organization");
        var establishment = model.Entity<Establishment>();
        establishment.ToTable("Establishments", "organization");
        establishment.Property(x => x.Code).HasMaxLength(20);
        establishment.HasIndex(x => x.Code).IsUnique();
        establishment.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId);
        var series = model.Entity<DocumentSeries>();
        series.ToTable("DocumentSeries", "organization");
        series.Property(x => x.Code).HasMaxLength(12);
        series.Property(x => x.DocumentType).HasMaxLength(16);
        series.HasIndex(x => new { x.DocumentType, x.Code }).IsUnique();
        series.HasOne<Establishment>().WithMany().HasForeignKey(x => x.EstablishmentId);
        branch.HasIndex(x => x.Name).IsUnique();
        var grant = model.Entity<BranchGrant>();
        grant.HasKey(x => new { x.UserId, x.BranchId });
        grant.Property(x => x.Role).HasMaxLength(40);
        grant.HasOne<Operator>().WithMany().HasForeignKey(x => x.UserId);
        grant.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId);
        var master = model.Entity<MasterRecord>();
        master.ToTable("Masters", "catalog");
        master.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        master.Property(x => x.Name).HasMaxLength(180);
        master.Property(x => x.Kind).HasMaxLength(16);
        master.Property(x => x.Code).HasMaxLength(40);
        master.Property(x => x.Version).IsRowVersion();
        master.Property(x => x.Conversion).HasPrecision(16, 6);
        master.Property(x => x.Price).HasPrecision(16, 2);
        master.Property(x => x.TaxRate).HasPrecision(5, 2);
        master.HasIndex(x => new { x.BranchId, x.Kind, x.Code }).IsUnique();
        var audit = model.Entity<AuditEntry>();
        audit.ToTable("Entries", "audit");
        audit.HasIndex(x => x.Time);
    }
}
