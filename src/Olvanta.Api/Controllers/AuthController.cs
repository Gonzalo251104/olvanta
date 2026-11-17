using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Olvanta.Api.Security;
using Olvanta.Infrastructure.Persistence;

namespace Olvanta.Api.Controllers;

public sealed record LoginRequest([Required, MaxLength(100)] string UserName, [Required, MaxLength(256)] string Password);
public sealed record CsrfToken(string Token);
public sealed record PasswordChange(string CurrentPassword, [MinLength(12), MaxLength(128)] string NewPassword);

[ApiController, Route("api/v1/auth"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(IAntiforgery csrf, SignInManager<Operator> signIn, SessionAccess access,
    UserManager<Operator> users) : ControllerBase
{
    [HttpGet("csrf")]
    public CsrfToken Token() => new(csrf.GetAndStoreTokens(HttpContext).RequestToken!);

    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Login(LoginRequest input)
    {
        var result = await signIn.PasswordSignInAsync(input.UserName.Trim(), input.Password, false, true);
        return result.Succeeded ? NoContent() : Unauthorized();
    }

    [Authorize, HttpGet("session")]
    public Task<SessionView> Session() => access.DescribeAsync();

    [Authorize, HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return NoContent();
    }

    [Authorize, HttpPost("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword(PasswordChange input)
    {
        var user = (await users.GetUserAsync(User))!;
        var result = await users.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword);
        if (!result.Succeeded) return Problem(statusCode: 400, detail: "Compruebe la contraseña actual y la complejidad de la nueva.");
        await signIn.RefreshSignInAsync(user);
        return NoContent();
    }
}
