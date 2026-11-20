using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Olvanta.Api;
using Olvanta.Api.Security;
using Olvanta.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("Olvanta")
    ?? throw new InvalidOperationException("ConnectionStrings:Olvanta is required.");
builder.Services.AddDbContext<OlvantaDbContext>(options => options.UseSqlServer(connection));
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<SessionAccess>();
builder.Services.AddScoped<IAuthorizationHandler, AdminHandler>();
builder.Services.AddAuthorization(options => options.AddPolicy("administration",
    policy => policy.RequireAuthenticatedUser().AddRequirements(new AdminRequirement())));
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.AddIdentityCore<Operator>(o =>
{
    o.Password.RequiredLength = 12;
    o.Password.RequireNonAlphanumeric = false;
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<OlvantaDbContext>().AddSignInManager().AddDefaultTokenProviders();
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);
var secure = builder.Configuration.GetValue<bool>("Security:LocalHttp") ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "olvanta.session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = secure;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.Name = "olvanta.csrf";
    o.Cookie.SecurePolicy = secure;
    o.Cookie.SameSite = SameSiteMode.Strict;
});
var protection = builder.Services.AddDataProtection().SetApplicationName("Olvanta");
if (builder.Configuration["Security:KeyPath"] is string keys)
    protection.PersistKeysToFileSystem(new DirectoryInfo(keys));
builder.Services.AddExceptionHandler<DataConflictHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi("v1", options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "Olvanta API";
    document.Info.Version = "0.4.0";
    document.Servers = [new Microsoft.OpenApi.OpenApiServer { Url = "/" }];
    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
    {
        ["session"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Cookie, Name = "olvanta.session" },
        ["csrf"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header, Name = "X-CSRF-TOKEN", Description = "Solicite un token en GET /api/v1/auth/csrf con la sesión actual." }
    };
    foreach (var (path, endpoint) in document.Paths)
    {
        foreach (var (method, operation) in endpoint.Operations!)
        {
            var requirements = new OpenApiSecurityRequirement();
            if (path is not ("/api/v1/auth/login" or "/api/v1/auth/csrf" or "/api/v1/system/status"))
                requirements[new OpenApiSecuritySchemeReference("session", document)] = [];
            if (method != HttpMethod.Get) requirements[new OpenApiSecuritySchemeReference("csrf", document)] = [];
            if (requirements.Count > 0) operation.Security = [requirements];
        }
    }
    return Task.CompletedTask;
}));

var app = builder.Build();
if (args.Contains("--initialize"))
{
    await IdentitySetup.InitializeAsync(app.Services, app.Configuration);
    return;
}
app.UseExceptionHandler();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true) context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseAuthorization();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapControllers();
app.MapOpenApi();
app.Run();
