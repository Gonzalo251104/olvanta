using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Olvanta.Api.Controllers;
using Olvanta.Application;
using Xunit;

namespace Olvanta.FoundationTests;

public class SystemControllerTests
{
    [Theory]
    [InlineData(HealthStatus.Healthy, 200, "ready", "connected")]
    [InlineData(HealthStatus.Unhealthy, 503, "degraded", "unavailable")]
    public async Task ReportsDatabaseAvailability(HealthStatus health, int code, string status, string database)
    {
        var controller = new SystemController(new FixedHealthService(health));
        var action = await controller.GetStatus(CancellationToken.None);
        var result = Assert.IsAssignableFrom<ObjectResult>(action.Result);
        Assert.Equal(code, result.StatusCode);
        Assert.Equal(new SystemStatus(status, database), result.Value);
    }

    private sealed class FixedHealthService(HealthStatus status) : HealthCheckService
    {
        public override Task<HealthReport> CheckHealthAsync(
            Func<HealthCheckRegistration, bool>? predicate, CancellationToken cancellationToken = default)
        {
            var entry = new HealthReportEntry(status, null, TimeSpan.Zero, null, null);
            return Task.FromResult(new HealthReport(new Dictionary<string, HealthReportEntry>
            {
                ["database"] = entry
            }, TimeSpan.Zero));
        }
    }
}
