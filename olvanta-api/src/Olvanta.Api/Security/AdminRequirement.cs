using Microsoft.AspNetCore.Authorization;

namespace Olvanta.Api.Security;

public sealed class AdminRequirement : IAuthorizationRequirement;

public sealed class AdminHandler(SessionAccess access) : AuthorizationHandler<AdminRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true && await access.IsAdministratorAsync())
            context.Succeed(requirement);
    }
}
