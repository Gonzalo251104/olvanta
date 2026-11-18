using Microsoft.AspNetCore.Identity;

namespace Olvanta.Infrastructure.Persistence;

public sealed class Operator : IdentityUser
{
    public bool IsAdministrator { get; set; }
}

public sealed class BranchGrant
{
    public required string UserId { get; set; }
    public int BranchId { get; set; }
    public required string Role { get; set; }
}
