using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Olvanta.Application;
using Olvanta.Domain;
using Olvanta.Infrastructure.Persistence;

namespace Olvanta.Api.Security;

public sealed record BranchAccess(int Id, string Name, string Currency, decimal TaxRate, string Role, string[] Permissions);
public sealed record SessionView(string UserName, bool Administrator, IReadOnlyList<BranchAccess> Branches);

public sealed class SessionAccess(OlvantaDbContext db, UserManager<Operator> users, IHttpContextAccessor http)
{
    public string UserId => users.GetUserId(http.HttpContext!.User)!;
    public string Actor => http.HttpContext!.User.Identity!.Name!;
    public Task<bool> IsAdministratorAsync() => db.Users.AnyAsync(u => u.Id == UserId && u.IsAdministrator);

    public async Task<bool> AllowsAsync(int branchId, string permission)
    {
        if (await IsAdministratorAsync()) return await db.Branches.AnyAsync(b => b.Id == branchId);
        var role = await db.Grants.Where(g => g.UserId == UserId && g.BranchId == branchId)
            .Select(g => g.Role).SingleOrDefaultAsync();
        return role != null && AccessRules.Roles.TryGetValue(role, out var permissions) && permissions.Contains(permission);
    }

    public async Task<SessionView> DescribeAsync()
    {
        var admin = await IsAdministratorAsync();
        var query = from branch in db.Branches
                    join grant in db.Grants.Where(g => g.UserId == UserId) on branch.Id equals grant.BranchId into grants
                    from grant in grants.DefaultIfEmpty()
                    where admin || grant != null
                    orderby branch.Name
                    select new { branch.Id, branch.Name, branch.Currency, branch.TaxRate, Role = admin ? "administrator" : grant!.Role };
        var branches = (await query.ToListAsync()).Select(b => new BranchAccess(b.Id, b.Name, b.Currency, b.TaxRate, b.Role,
            admin ? AccessRules.All : AccessRules.Roles[b.Role])).ToList();
        return new SessionView(Actor, admin, branches);
    }

    public void Record(string action, string detail, int? branchId = null) =>
        db.Audit.Add(new AuditEntry { Actor = Actor, Action = action, Detail = detail, BranchId = branchId });
}
