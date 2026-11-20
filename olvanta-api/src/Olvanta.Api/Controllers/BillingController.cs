using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Olvanta.Api.Billing;
using Olvanta.Api.Security;
using Olvanta.Application;
using Olvanta.Domain;
using Olvanta.Infrastructure.Persistence;

namespace Olvanta.Api.Controllers;

[Authorize, ApiController, Route("api/v1/branches/{branchId:int}/billing")]
public sealed class BillingController(OlvantaDbContext db, SessionAccess access, FiscalSigner signer, FiscalIssuer issuer, FiscalDelivery delivery) : ControllerBase
{
    private static readonly Expression<Func<FiscalDocument, BillRow>> Row = x => new BillRow(x.Id, x.SaleId, x.ParentId,
        x.Type, x.Identifier, x.Status, x.Mode, x.Created, x.Cdr != null);

    [HttpGet]
    public async Task<ActionResult<BillingDesk>> Desk(int branchId)
    {
        if (!await access.AllowsAsync(branchId, AccessRules.BillingRead)) return Forbid();
        var sales = await db.Sales.Where(x => x.BranchId == branchId && (x.Stage == "confirmed" || x.Stage == "dispatched"))
            .OrderByDescending(x => x.Id).Select(x => new BillableSale(x.Id, x.Customer, x.Currency, x.Lines.Sum(l => l.Total),
                db.SalesEvents.Where(e => e.SaleId == x.Id && e.Kind == "return").Select(e => new BillableReturn(e.Id, e.LineId!.Value, e.Amount, e.Note)).ToList())).ToListAsync();
        var series = await (from s in db.Series
                            join site in db.Establishments on s.EstablishmentId equals site.Id
                            where site.BranchId == branchId
                            orderby s.Code
                            select new FiscalSeries(s.Id, s.DocumentType, s.Code, site.Name)).ToListAsync();
        var documents = await db.FiscalDocuments.Where(x => x.BranchId == branchId).OrderByDescending(x => x.Id).Select(Row).ToListAsync();
        return new BillingDesk(signer.Mode, series, sales, documents);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BillDetail>> Detail(int branchId, int id)
    {
        if (!await access.AllowsAsync(branchId, AccessRules.BillingRead)) return Forbid();
        var scope = db.FiscalDocuments.Where(x => x.BranchId == branchId && x.Id == id);
        var row = await scope.Select(Row).SingleOrDefaultAsync();
        if (row == null) return NotFound();
        var content = await scope.Select(x => new { x.Snapshot, x.Digest, x.Ticket }).SingleAsync();
        var history = await db.FiscalAttempts.Where(x => x.DocumentId == id).OrderBy(x => x.Id)
            .Select(x => new AttemptView(x.Id, x.Time, x.Actor, x.Action, x.Outcome, x.Detail, x.Response != null)).ToListAsync();
        return new BillDetail(row, System.Text.Json.JsonSerializer.Deserialize<FiscalSnapshot>(content.Snapshot)!, content.Digest, content.Ticket, history);
    }

    [HttpPost]
    public async Task<ActionResult<int>> Prepare(int branchId, FiscalInput input)
    {
        if (!await access.AllowsAsync(branchId, AccessRules.Bill)) return Forbid();
        return await issuer.Prepare(branchId, input);
    }

    [HttpPost("{id:int}/submission"), ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Submission(int branchId, int id, SubmissionInput input)
    {
        if (!await access.AllowsAsync(branchId, AccessRules.Bill)) return Forbid();
        await delivery.Process(branchId, id, input);
        return NoContent();
    }

    [HttpGet("{id:int}/files/{kind}")]
    public async Task<IActionResult> Download(int branchId, int id, string kind, long? attemptId)
    {
        if (!await access.AllowsAsync(branchId, AccessRules.BillingRead)) return Forbid();
        var scope = db.FiscalDocuments.Where(x => x.Id == id && x.BranchId == branchId);
        var stem = await scope.Select(x => x.FileStem).SingleOrDefaultAsync();
        if (stem == null) return NotFound();
        if (kind == "response")
        {
            var response = await db.FiscalAttempts.Where(x => x.DocumentId == id && x.Id == attemptId).Select(x => x.Response).SingleOrDefaultAsync();
            return response == null ? NotFound() : File(response, "application/octet-stream", stem + "-respuesta-" + attemptId + (response[0] == (byte)'P' ? ".zip" : ".xml"));
        }
        var bytes = kind switch
        {
            "xml" => await scope.Select(x => x.Xml).SingleAsync(),
            "zip" => await scope.Select(x => x.Zip).SingleAsync(),
            "pdf" => await scope.Select(x => x.Pdf).SingleAsync(),
            "cdr" => await scope.Select(x => x.Cdr).SingleAsync(),
            _ => null
        };
        if (bytes == null) return NotFound();
        return File(bytes, kind switch { "pdf" => "application/pdf", "xml" => "application/xml", _ => "application/zip" },
            kind == "cdr" ? "R-" + stem + ".zip" : stem + "." + kind);
    }
}
