using System.Security.Claims;

namespace Shared.Identity;

public static class ClaimsPrincipalExtensions
{
    // The "name" claim is always present on the access token and equals ApplicationUser.UserName
    // (the bank code, e.g. "P5229"); preferred_username carries the same value but only ships when
    // the "profile" scope is requested. Resolve name first so audit stamping never falls back to
    // "system" unintentionally. Shared by CurrentUserService.UserCode and HttpUserEnricher — was
    // two copies of this chain, now one. (NotificationHub.ResolveUsername has its own third copy,
    // untouched here — out of scope for this pass.)
    public static string? GetUserCode(this ClaimsPrincipal? user) =>
        user?.FindFirst("name")?.Value
        ?? user?.FindFirst(ClaimTypes.Name)?.Value
        ?? user?.FindFirst("preferred_username")?.Value;
}
