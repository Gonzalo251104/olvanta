using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Olvanta.Api.Security;
using Olvanta.Application;
using Olvanta.Domain;
using Olvanta.Infrastructure.Persistence;

namespace Olvanta.Api.Controllers;

public sealed record BranchInput([Required, MaxLength(100)] string Name);
public sealed record GrantInput(int BranchId, [Required] string Role);
public sealed record UserInput([Required, MaxLength(100)] string UserName, [MinLength(12), MaxLength(128)] string Password, GrantInput[] Grants);
public sealed record GrantChange(string Version, GrantInput[] Grants);
public sealed record ResetInput([MinLength(12), MaxLength(128)] string NewPassword);
public sealed record OperatorView(string Id, string UserName, bool Administrator, string Version, GrantInput[] Grants);
public sealed record BranchView(int Id, string Name);
public sealed record AuditView(DateTimeOffset Time, string Actor, string Action, string Detail, int? BranchId);

[Authorize(Policy = "administration"), ApiController, Route("api/v1/admin")]
public sealed class AdministrationController(OlvantaDbContext db, UserManager<Operator> users, SessionAccess access) : ControllerBase
{
    [HttpGet("roles")]
    public IReadOnlyDictionary<string, string[]> Roles() => AccessRules.Roles;

    [HttpGet("branches")]
    public Task<List<BranchView>> Branches() => db.Branches.OrderBy(b => b.Name).Select(b => new BranchView(b.Id, b.Name)).ToListAsync();

    [HttpPost("branches")]
    public async Task<ActionResult<BranchView>> CreateBranch(BranchInput input)
    {
        var branch = new Branch { Name = input.Name.Trim() };
        db.Branches.Add(branch);
        access.Record("branch.created", branch.Name);
        await db.SaveChangesAsync();
        return Ok(new BranchView(branch.Id, branch.Name));
    }

    [HttpPut("branches/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RenameBranch(int id, BranchInput input)
    {
        var branch = await db.Branches.FindAsync(id);
        if (branch == null) return NotFound();
        access.Record("branch.renamed", branch.Name + " → " + input.Name.Trim(), id);
        branch.Name = input.Name.Trim();
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("users")]
    public async Task<List<OperatorView>> Users()
    {
        var grants = (await db.Grants.AsNoTracking().ToListAsync()).ToLookup(g => g.UserId);
        var operators = await db.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync();
        return operators.Select(u => new OperatorView(u.Id, u.UserName!, u.IsAdministrator, u.ConcurrencyStamp!,
            grants[u.Id].Select(g => new GrantInput(g.BranchId, g.Role)).ToArray())).ToList();
    }

    [HttpPost("users")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Create(UserInput input)
    {
        if (!await ValidGrants(input.Grants)) return Problem(statusCode: 400, detail: "Revise las sucursales y los roles.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = new Operator { UserName = input.UserName.Trim() };
        var result = await users.CreateAsync(user, input.Password);
        if (!result.Succeeded) return IdentityError(result);
        Assign(user, input.Grants);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return NoContent();
    }

    [HttpPut("users/{id}/grants")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangeGrants(string id, GrantChange input)
    {
        if (!await ValidGrants(input.Grants)) return Problem(statusCode: 400, detail: "Revise las sucursales y los roles.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = await users.FindByIdAsync(id);
        if (user == null) return NotFound();
        if (user.IsAdministrator) return Forbid();
        if (user.ConcurrencyStamp != input.Version) return Conflict();
        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded) return Conflict();
        await db.Grants.Where(g => g.UserId == id).ExecuteDeleteAsync();
        Assign(user, input.Grants);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return NoContent();
    }

    [HttpPost("users/{id}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Reset(string id, ResetInput input)
    {
        var user = await users.FindByIdAsync(id);
        if (user == null) return NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var result = await users.ResetPasswordAsync(user, await users.GeneratePasswordResetTokenAsync(user), input.NewPassword);
        if (!result.Succeeded) return IdentityError(result);
        var unlocked = await users.SetLockoutEndDateAsync(user, null);
        if (!unlocked.Succeeded) return IdentityError(unlocked);
        access.Record("user.password-reset", user.UserName!);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return NoContent();
    }

    [HttpGet("audit")]
    public Task<List<AuditView>> Audit() => db.Audit.OrderByDescending(a => a.Id).Take(100)
        .Select(a => new AuditView(a.Time, a.Actor, a.Action, a.Detail, a.BranchId)).ToListAsync();

    private async Task<bool> ValidGrants(GrantInput[] grants)
    {
        if (grants.Length > 200 || grants.Any(g => g == null || !AccessRules.Roles.ContainsKey(g.Role))) return false;
        var ids = grants.Select(g => g.BranchId).Distinct().ToArray();
        return ids.Length == grants.Length && await db.Branches.CountAsync(b => ids.Contains(b.Id)) == ids.Length;
    }

    private void Assign(Operator user, GrantInput[] grants)
    {
        foreach (var grant in grants)
            db.Grants.Add(new BranchGrant { UserId = user.Id, BranchId = grant.BranchId, Role = grant.Role });
        var detail = string.Join(", ", grants.Select(g => $"{g.BranchId}={g.Role}"));
        access.Record("user.grants-set", $"{user.UserName} [{detail}]");
    }

    private ObjectResult IdentityError(IdentityResult result) => Problem(statusCode: 400,
        detail: result.Errors.Any(e => e.Code.Contains("UserName")) ? "Nombre de usuario inválido o en uso." :
        "La contraseña requiere 12 caracteres, mayúsculas, minúsculas y números.");
}
