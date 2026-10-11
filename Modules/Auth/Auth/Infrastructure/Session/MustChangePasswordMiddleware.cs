using Microsoft.AspNetCore.Http;

namespace Auth.Infrastructure.Session;

/// <summary>
/// Server-side half of the forced password change: while a browser session owes a change, every endpoint answers
/// 403 unless it is marked <see cref="AllowWhilePasswordChangeRequiredAttribute"/>. The SPA gate alone is only
/// cosmetic.
/// </summary>
/// <remarks>
/// Bearer callers are not affected — only principals built from the app cookie carry the claim. A request that
/// matched no endpoint has nothing to protect and passes. The OIDC callbacks never get here: the authentication
/// middleware answers them first.
/// </remarks>
public sealed class MustChangePasswordMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.User;
        if (user.Identity?.AuthenticationType == AuthSchemes.AppCookie
            && user.HasClaim(AuthSchemes.MustChangePasswordClaim, "true")
            && context.GetEndpoint() is { } endpoint
            && !IsAllowed(context, endpoint))
        {
            await Results.Problem(
                    title: "Password change required",
                    detail: "Change your password before using the system.",
                    statusCode: StatusCodes.Status403Forbidden)
                .ExecuteAsync(context);
            return;
        }

        await next(context);
    }

    private static bool IsAllowed(HttpContext context, Endpoint endpoint)
    {
        var marker = endpoint.Metadata.GetMetadata<AllowWhilePasswordChangeRequiredAttribute>();
        if (marker is null) return false;
        if (marker.SelfRouteValue is null) return true;

        var self = context.User.FindFirst("sub")?.Value;
        return self is not null
               && string.Equals(context.GetRouteValue(marker.SelfRouteValue)?.ToString(), self,
                   StringComparison.OrdinalIgnoreCase);
    }
}
