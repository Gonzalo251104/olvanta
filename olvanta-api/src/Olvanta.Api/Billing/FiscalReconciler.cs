using Microsoft.EntityFrameworkCore;
using Olvanta.Application;
using Olvanta.Infrastructure.Persistence;

namespace Olvanta.Api.Billing;

public sealed class FiscalReconciler(IServiceScopeFactory scopes, FiscalSigner signer, ILogger<FiscalReconciler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<OlvantaDbContext>();
                var now = DateTimeOffset.UtcNow;
                var due = await db.FiscalDocuments.AsNoTracking()
                    .Where(d => d.Mode == signer.Mode && d.NextQuery <= now && (d.LeaseUntil == null || d.LeaseUntil <= now))
                    .OrderBy(d => d.NextQuery).ThenBy(d => d.Id)
                    .Select(d => new { d.Id, d.BranchId }).FirstOrDefaultAsync(stoppingToken);
                if (due != null)
                    await scope.ServiceProvider.GetRequiredService<FiscalDelivery>()
                        .Process(due.BranchId, due.Id, new SubmissionInput("query"), "Conciliación programada", scheduled: true);
            }
            catch (TradeException)
            {
                // A foreground query or another instance can claim the document first.
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Conciliación fiscal aplazada. Error de tipo {ErrorType}.", error.GetType().Name);
            }
        }
    }
}
