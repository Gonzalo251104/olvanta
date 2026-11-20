using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Olvanta.Api.Security;
using Olvanta.Domain;
using Olvanta.Infrastructure.Persistence;

namespace Olvanta.Api.Controllers;

public sealed record CompanyInput([Required, MaxLength(180)] string Name, [RegularExpression("[0-9]{11}")] string? TaxId);
public sealed record BranchDefaults([RegularExpression("PEN|USD")] string Currency, [Range(0, 100)] decimal TaxRate);
public sealed record SiteInput(int BranchId, [Required, MaxLength(100)] string Name, [Required, MaxLength(20)] string Code, [Required, MaxLength(300)] string Address);
public sealed record SiteView(int Id, int BranchId, string Name, string Code, string Address);
public sealed record SeriesInput(int EstablishmentId, [RegularExpression("invoice|receipt|credit|debit")] string DocumentType, [RegularExpression("[A-Z0-9]{1,12}")] string Code);
public sealed record SeriesView(int Id, int EstablishmentId, string DocumentType, string Code);
public sealed record OrganizationView(CompanyInput Company, List<SiteView> Establishments, List<SeriesView> Series);

[Authorize(Policy = "administration"), ApiController, Route("api/v1/admin/organization")]
public sealed class OrganizationController(OlvantaDbContext db, SessionAccess access) : ControllerBase
{
    [HttpGet]
    public async Task<OrganizationView> Get() => new(
        await db.Companies.Select(c => new CompanyInput(c.Name, c.TaxId)).SingleAsync(),
        await db.Establishments.OrderBy(s => s.Name).Select(s => new SiteView(s.Id, s.BranchId, s.Name, s.Code, s.Address)).ToListAsync(),
        await db.Series.OrderBy(s => s.Code).Select(s => new SeriesView(s.Id, s.EstablishmentId, s.DocumentType, s.Code)).ToListAsync());

    [HttpPut("company"), ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Company(CompanyInput input)
    {
        var company = await db.Companies.SingleAsync();
        company.Name = input.Name.Trim();
        company.TaxId = input.TaxId;
        access.Record("company.updated", company.Name);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("branches/{branchId:int}/defaults"), ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Defaults(int branchId, BranchDefaults input)
    {
        if (decimal.Round(input.TaxRate, 2) != input.TaxRate) return BadRequest();
        await using var tx = await db.Database.BeginTransactionAsync();
        var branch = await db.LockBranchAsync(branchId);
        if (branch == null) return NotFound();
        if (input.Currency != branch.Currency && (await db.Purchases.AnyAsync(x => x.BranchId == branchId) ||
            await db.Movements.AnyAsync(x => x.BranchId == branchId) || await db.Sales.AnyAsync(x => x.BranchId == branchId) ||
            await db.CashSessions.AnyAsync(x => x.BranchId == branchId)))
            return Problem(statusCode: 409, detail: "La sucursal ya registra operaciones en su moneda actual.");
        branch.Currency = input.Currency;
        branch.TaxRate = input.TaxRate;
        access.Record("branch.defaults-set", $"{input.Currency} · {input.TaxRate}%", branch.Id);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return NoContent();
    }

    [HttpPost("establishments"), ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddSite(SiteInput input)
    {
        if (!await db.Branches.AnyAsync(b => b.Id == input.BranchId)) return NotFound();
        db.Establishments.Add(new Establishment { BranchId = input.BranchId, Name = input.Name.Trim(), Code = input.Code.Trim().ToUpperInvariant(), Address = input.Address.Trim() });
        access.Record("establishment.created", input.Code, input.BranchId);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("series"), ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddSeries(SeriesInput input)
    {
        var site = await db.Establishments.FindAsync(input.EstablishmentId);
        if (site == null) return NotFound();
        db.Series.Add(new DocumentSeries { EstablishmentId = site.Id, Code = input.Code, DocumentType = input.DocumentType });
        access.Record("series.created", $"{input.DocumentType} · {input.Code}", site.BranchId);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
