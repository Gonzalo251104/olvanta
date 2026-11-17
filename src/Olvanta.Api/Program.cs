using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Olvanta.Api;
using Olvanta.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("Olvanta")
    ?? throw new InvalidOperationException("ConnectionStrings:Olvanta is required.");
builder.Services.AddDbContext<OlvantaDbContext>(options => options.UseSqlServer(connection));
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi("v1", options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "Olvanta API";
    document.Info.Version = "0.1.0";
    document.Servers = [new Microsoft.OpenApi.OpenApiServer { Url = "/" }];
    return Task.CompletedTask;
}));

var app = builder.Build();
app.UseExceptionHandler();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapControllers();
app.MapOpenApi();
app.Run();
