using System.Xml;
using Microsoft.EntityFrameworkCore;
using Olvanta.Api.Security;
using Olvanta.Application;
using Olvanta.Domain;
using Olvanta.Infrastructure.Persistence;

namespace Olvanta.Api.Billing;

public sealed class FiscalDelivery(OlvantaDbContext db, SessionAccess access, FiscalSigner signer, SimulatedGateway simulator, BetaGateway beta)
{
    public async Task Process(int branchId, int id, SubmissionInput input)
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
            if (doc.LeaseUntil > DateTimeOffset.UtcNow) throw new TradeException("Ya existe una operación en curso.");
            if (query ? doc.Status is not ("uncertain" or "sending" or "processing") : doc.Status != "prepared")
                throw new TradeException("La acción no corresponde al estado del documento.");
            doc.Lease = lease;
            doc.LeaseUntil = DateTimeOffset.UtcNow.AddMinutes(2);
            doc.Status = query ? "uncertain" : "sending";
            attempt = new FiscalAttempt { DocumentId = id, Actor = access.Actor, Action = input.Action, Outcome = "started", Detail = "Intento iniciado." };
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
            if (outcome.Status == "accepted" && doc.Type is ("RC" or "RA"))
            {
                var parent = await db.FiscalDocuments.SingleAsync(x => x.Id == doc.ParentId && x.BranchId == branchId);
                if (FiscalIssuer.Read(doc).ReasonCode == "void") { parent.Status = "voided"; parent.Active = false; }
                else parent.Status = "accepted";
            }
        }
        access.Record("billing." + input.Action, $"{doc.Mode} · {doc.FileStem} · {outcome.Status}", branchId);
        await db.SaveChangesAsync();
        await complete.CommitAsync();
    }
}
