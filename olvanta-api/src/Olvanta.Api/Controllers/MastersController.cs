using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Olvanta.Api.Security;
using Olvanta.Application;
using Olvanta.Domain;
using Olvanta.Infrastructure.Persistence;

namespace Olvanta.Api.Controllers;

[Authorize, ApiController, Route("api/v1/branches/{branchId:int}/masters/{kind}")]
public sealed class MastersController(OlvantaDbContext db, SessionAccess access) : ControllerBase
{
    private IQueryable<MasterRecord> Scope(int branchId, string kind) =>
        db.Masters.Where(x => x.BranchId == branchId && x.Kind == kind);

    [HttpGet]
    public async Task<ActionResult<MasterPage>> List(int branchId, string kind, [MaxLength(180)] string? search = null,
        bool? active = null, [Range(1, 1000000)] int page = 1, [Range(1, 100)] int pageSize = 20)
    {
        if (!MasterInput.IsKind(kind)) return NotFound();
        if (!await access.AllowsAsync(branchId, AccessRules.Read)) return Forbid();
        var query = Scope(branchId, kind).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Name.Contains(term) || x.Code.Contains(term));
        }
        if (active.HasValue) query = query.Where(x => x.Active == active);
        var total = await query.CountAsync();
        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((page - 1) * pageSize)
            .Take(pageSize).Select(MasterView.Project).ToListAsync();
        return new MasterPage(total, page, pageSize, items);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MasterView>> Get(int branchId, string kind, int id)
    {
        if (!await access.AllowsAsync(branchId, AccessRules.Read)) return Forbid();
        var entry = await Scope(branchId, kind).Where(x => x.Id == id).Select(MasterView.Project).SingleOrDefaultAsync();
        return entry == null ? NotFound() : Ok(entry);
    }

    [HttpGet("{id:int}/history")]
    public async Task<ActionResult<List<AuditView>>> History(int branchId, string kind, int id)
    {
        if (!await access.AllowsAsync(branchId, AccessRules.Read)) return Forbid();
        if (!await Scope(branchId, kind).AnyAsync(x => x.Id == id)) return NotFound();
        return await db.Audit.Where(x => x.BranchId == branchId && x.MasterId == id).OrderByDescending(x => x.Id)
            .Select(x => new AuditView(x.Time, x.Actor, x.Action, x.Detail, x.BranchId)).Take(100).ToListAsync();
    }

    [HttpPost, ProducesResponseType<MasterView>(StatusCodes.Status201Created)]
    public async Task<ActionResult<MasterView>> Create(int branchId, string kind, MasterInput input)
    {
        var rejected = await CheckWrite(branchId, kind, input);
        if (rejected != null) return rejected;
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.LockBranchAsync(branchId);
        var record = Add(branchId, kind, input);
        await db.SaveChangesAsync();
        Audit(record, "master.created");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return CreatedAtAction(nameof(Get), new { branchId, kind, id = record.Id }, MasterView.Project.Compile()(record));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(int branchId, string kind, int id, MasterInput input)
    {
        var rejected = await CheckWrite(branchId, kind, input);
        if (rejected != null) return rejected;
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.LockBranchAsync(branchId);
        var record = await Scope(branchId, kind).SingleOrDefaultAsync(x => x.Id == id);
        if (record == null) return NotFound();
        if (input.Version?.Length != 8) return Problem(statusCode: 400, detail: "Se requiere la versión del registro.");
        if (!record.Version.SequenceEqual(input.Version)) return Conflict();
        if (kind == "products" && input.Unit?.Trim() != record.Unit &&
            (await db.PurchaseLines.AnyAsync(x => x.ProductId == id) || await db.Movements.AnyAsync(x => x.ProductId == id) ||
            await db.Minimums.AnyAsync(x => x.ProductId == id) || await db.Set<SaleLine>().AnyAsync(x => x.ItemId == id)))
            return Problem(statusCode: 409, detail: "El producto ya tiene operaciones registradas en su unidad base.");
        input.Apply(record);
        db.Entry(record).Property(x => x.Name).IsModified = true;
        Audit(record, "master.updated");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return NoContent();
    }

    [HttpPost("import")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Import(int branchId, string kind, MasterInput[] rows)
    {
        if (!MasterInput.IsParty(kind)) return NotFound();
        if (!await access.AllowsAsync(branchId, AccessRules.Write)) return Forbid();
        if (rows.Length is < 1 or > 200) return Problem(statusCode: 400, detail: "Importe entre 1 y 200 filas.");
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < rows.Length; i++)
        {
            var error = rows[i]?.ValidateFor(kind);
            if (rows[i] == null || error != null) return Problem(statusCode: 400, detail: $"Fila {i + 1}. {error ?? "Fila vacía."}");
            if (!codes.Add(rows[i].Code.Trim().ToUpperInvariant())) return Problem(statusCode: 400, detail: $"Fila {i + 1}. Identificador repetido en el archivo.");
        }
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.LockBranchAsync(branchId);
        var existing = await Scope(branchId, kind).Where(x => codes.Contains(x.Code)).Select(x => x.Code).ToListAsync();
        if (existing.Count > 0)
        {
            var first = Array.FindIndex(rows, r => r.Code.Trim().Equals(existing[0], StringComparison.OrdinalIgnoreCase));
            return Problem(statusCode: 409, detail: $"Fila {first + 1}. Ya existe el identificador {existing[0]} en esta sucursal.");
        }
        await SaveImport(branchId, kind, rows);
        await tx.CommitAsync();
        return NoContent();
    }

    [HttpPost("import/csv"), RequestSizeLimit(3_200_000)]
    public async Task<ActionResult<PartyCsvResult>> Csv(int branchId, string kind, PartyCsvInput input)
    {
        if (!MasterInput.IsParty(kind)) return NotFound();
        if (!await access.AllowsAsync(branchId, AccessRules.Write)) return Forbid();
        var (rows, issues) = PartyImport.Parse(input.Content, kind);
        await using var tx = await db.Database.BeginTransactionAsync();
        if (input.Commit) await db.LockBranchAsync(branchId);
        var preview = rows.Select(r => new CsvParty(r.Row, r.Data.Code.Trim().ToUpperInvariant(), r.Data.Name.Trim())).ToList();
        var codes = preview.Select(r => r.Code).Distinct().ToArray();
        var existing = (await Scope(branchId, kind).Where(m => codes.Contains(m.Code)).Select(m => m.Code).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in preview)
        {
            if (!seen.Add(row.Code)) issues.Add(new(row.Row, "Identificador repetido en el archivo."));
            if (existing.Contains(row.Code)) issues.Add(new(row.Row, "El identificador ya existe en esta sucursal."));
        }
        int imported = 0;
        if (input.Commit && issues.Count == 0)
        {
            await SaveImport(branchId, kind, rows.Select(r => r.Data));
            imported = rows.Count;
        }
        await tx.CommitAsync();
        return new PartyCsvResult(preview, issues.OrderBy(e => e.Row).ToList(), imported);
    }

    private async Task SaveImport(int branchId, string kind, IEnumerable<MasterInput> rows)
    {
        var records = rows.Select(row => Add(branchId, kind, row)).ToArray();
        await db.SaveChangesAsync();
        foreach (var record in records) Audit(record, "master.imported");
        await db.SaveChangesAsync();
    }

    private async Task<ActionResult?> CheckWrite(int branchId, string kind, MasterInput input)
    {
        if (!MasterInput.IsKind(kind)) return NotFound();
        if (!await access.AllowsAsync(branchId, AccessRules.Write)) return Forbid();
        var error = input.ValidateFor(kind);
        return error == null ? null : Problem(statusCode: 400, detail: error);
    }

    private MasterRecord Add(int branchId, string kind, MasterInput input)
    {
        var record = new MasterRecord { BranchId = branchId, Kind = kind, Name = "", Code = "" };
        input.Apply(record);
        db.Masters.Add(record);
        return record;
    }

    private void Audit(MasterRecord record, string action) => db.Audit.Add(new AuditEntry
    {
        Actor = access.Actor,
        Action = action,
        BranchId = record.BranchId,
        MasterId = record.Id,
        Detail = $"{record.Kind} · {record.Code} · {(record.Active ? "activo" : "inactivo")}"
    });
}
