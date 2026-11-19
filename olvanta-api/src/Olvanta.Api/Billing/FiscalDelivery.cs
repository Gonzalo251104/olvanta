using System.Xml;
using Microsoft.EntityFrameworkCore;
using Olvanta.Application;
using Olvanta.Domain;
using Olvanta.Infrastructure.Persistence;

namespace Olvanta.Api.Billing;

public sealed class FiscalDelivery(OlvantaDbContext db, FiscalSigner signer, SimulatedGateway simulator, BetaGateway beta)
{
    private const int MaximumQueries = 5;
    private static bool CanQuery(FiscalDocument doc) => doc.Status is "uncertain" or "sending" or "processing";

    public async Task Schedule(int branchId, int id, string actor)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.LockBranchAsync(branchId);
        var doc = await db.FiscalDocuments.SingleOrDefaultAsync(x => x.BranchId == branchId && x.Id == id)
            ?? throw new TradeException("Documento no encontrado.", 404);
        if (doc.Mode != signer.Mode || !CanQuery(doc) || doc.Mode == "beta" && string.IsNullOrEmpty(doc.Ticket))
            throw new TradeException("La consulta programada requiere un documento pendiente del entorno activo y un ticket en BETA.");
        if (doc.NextQuery != null) return;
        doc.NextQuery = DateTimeOffset.UtcNow.AddSeconds(3);
        doc.PlannedQueries = 0;
        db.Audit.Add(new AuditEntry { Actor = actor, Action = "billing.query-scheduled", Detail = doc.Identifier, BranchId = branchId });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task Process(int branchId, int id, SubmissionInput input, string actor, bool scheduled = false)
    {
        var query = input.Action == "query";
        if (!query && input.Action != "send") throw new TradeException("Seleccione enviar o consultar.", 400);
        var scenario = input.Scenario ?? "accept";
        if (scenario is not ("accept" or "reject" or "lost") || signer.Mode != "simulation" && scenario != "accept")
            throw new TradeException("Escenario de envío no válido.", 400);
        var lease = Guid.NewGuid();
        FiscalDocument doc;
        FiscalAttempt attempt;
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await db.LockBranchAsync(branchId);
            doc = await db.FiscalDocuments.SingleOrDefaultAsync(x => x.BranchId == branchId && x.Id == id)
                ?? throw new TradeException("Documento no encontrado.", 404);
            if (doc.Mode != signer.Mode) throw new TradeException("El entorno activo difiere del entorno de emisión.");
            if (scheduled && (doc.NextQuery == null || doc.NextQuery > DateTimeOffset.UtcNow || !CanQuery(doc))) return;
            if (doc.LeaseUntil > DateTimeOffset.UtcNow) throw new TradeException("Ya existe una operación en curso.");
            if (query ? !CanQuery(doc) : doc.Status != "prepared")
                throw new TradeException("La acción no corresponde al estado del documento.");
            if (scheduled && doc.PlannedQueries >= MaximumQueries)
            {
                doc.NextQuery = null;
                await db.SaveChangesAsync();
                await tx.CommitAsync();
                return;
            }
            if (scheduled) doc.PlannedQueries++;
            doc.Lease = lease;
            doc.LeaseUntil = DateTimeOffset.UtcNow.AddMinutes(2);
            doc.Status = query ? "uncertain" : "sending";
            attempt = new FiscalAttempt { DocumentId = id, Actor = actor, Action = input.Action, Outcome = "started", Detail = "Intento iniciado." };
            db.FiscalAttempts.Add(attempt);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        Delivery outcome;
        try
        {
            IFiscalGateway gateway = doc.Mode == "simulation" ? simulator : beta;
            outcome = await gateway.Exchange(doc, query, scenario);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or XmlException or InvalidDataException or FormatException or IOException or InvalidOperationException or TradeException)
        { outcome = new Delivery("uncertain", "No se pudo confirmar la respuesta del receptor. Consulte el estado antes de reenviar."); }
        await using var complete = await db.Database.BeginTransactionAsync();
        await db.LockBranchAsync(branchId);
        await db.Entry(doc).ReloadAsync();
        attempt.Outcome = outcome.Status;
        attempt.Detail = outcome.Detail;
        attempt.Response = outcome.Response;
        if (doc.Lease == lease)
        {
            doc.Status = outcome.Status;
            doc.Ticket = outcome.Ticket ?? doc.Ticket;
            doc.Cdr = outcome.Cdr ?? doc.Cdr;
            doc.Active = outcome.Status != "rejected";
            doc.Lease = null;
            doc.LeaseUntil = null;
            if (doc.NextQuery != null)
                doc.NextQuery = CanQuery(doc) && doc.PlannedQueries < MaximumQueries
                    ? DateTimeOffset.UtcNow.AddSeconds(15 * (1 << Math.Max(0, doc.PlannedQueries - 1))) : null;
            if (outcome.Status == "accepted" && doc.Type is ("RC" or "RA"))
            {
                var parent = await db.FiscalDocuments.SingleAsync(x => x.Id == doc.ParentId && x.BranchId == branchId);
                if (FiscalIssuer.Read(doc).ReasonCode == "void") { parent.Status = "voided"; parent.Active = false; }
                else parent.Status = "accepted";
            }
        }
        db.Audit.Add(new AuditEntry { Actor = actor, Action = "billing." + input.Action, Detail = $"{doc.Mode} · {doc.FileStem} · {outcome.Status}", BranchId = branchId });
        await db.SaveChangesAsync();
        await complete.CommitAsync();
    }
}
