using Microsoft.Extensions.Diagnostics.HealthChecks;
using Olvanta.Infrastructure.Persistence;

namespace Olvanta.Api;

public sealed class DatabaseHealthCheck(OlvantaDbContext database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken)
    {
        return await database.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy();
    }
}
