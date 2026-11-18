using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Olvanta.Domain;
using Olvanta.Infrastructure.Persistence;

namespace Olvanta.Api.Security;

public static class IdentitySetup
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OlvantaDbContext>();
        await db.Database.MigrateAsync();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (!await db.Branches.AnyAsync()) db.Branches.Add(new Branch { Name = "Principal" });
        if (!await db.Companies.AnyAsync()) db.Companies.Add(new Company { Name = "Olvanta" });
        if (!await db.Users.AnyAsync(u => u.IsAdministrator))
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<Operator>>();
            var user = new Operator { UserName = "admin", IsAdministrator = true };
            var password = config["Bootstrap:AdminPassword"] ?? throw new InvalidOperationException("Bootstrap password required");
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded) throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Code)));
            db.Audit.Add(new AuditEntry { Actor = "bootstrap", Action = "admin.created", Detail = user.Id });
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
