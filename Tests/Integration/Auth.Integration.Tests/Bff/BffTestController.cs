using System.Security.Claims;
using Auth.Infrastructure.Session;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Identity;

namespace Integration.Auth.Integration.Tests.Bff;

/// <summary>
/// Test-only endpoints, added to the host by <c>BffWebApplicationFactory</c>. <c>signin</c> mints a real
/// session (through the real cookie handler and ticket store) for a chosen user without the IdP round
/// trip; <c>whoami</c> reports what the application sees for the current request.
/// </summary>
[ApiController]
public class BffTestController(ICurrentUserService currentUser) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("/test/signin")]
    public async Task<IActionResult> SignIn(Guid userId, string name)
    {
        var identity = new ClaimsIdentity(AuthSchemes.AppCookie, "name", "roles");
        identity.AddClaim(new Claim("sub", userId.ToString()));
        identity.AddClaim(new Claim("name", name));
        identity.AddClaim(new Claim("preferred_username", name));
        await HttpContext.SignInAsync(AuthSchemes.AppCookie, new ClaimsPrincipal(identity));
        return Ok();
    }

    [HttpGet("/test/whoami")]
    public IActionResult WhoAmI() => Ok(new
    {
        authenticationType = User.Identity?.AuthenticationType,
        claims = User.Claims.Select(c => new { type = c.Type, value = c.Value }).ToList(),
        userCode = currentUser.UserCode,
        username = currentUser.Username,
        isExternal = currentUser.IsExternal,
        companyId = currentUser.CompanyId,
        roles = currentUser.Roles,
        permissions = currentUser.Permissions
    });

    [HttpPost("/test/echo")]
    public IActionResult Echo() => Ok();

    // Stands in for the Hangfire dashboard in the one place the real one cannot be reached: Development leaves the
    // dashboard anonymous. Same path prefix and same policy, so the cookie events treat it as the dashboard.
    [Authorize(Policy = "HangfireDashboard")]
    [HttpGet("/hangfire/probe")]
    [HttpPost("/hangfire/probe")]
    public IActionResult DashboardProbe() => Ok();
}
