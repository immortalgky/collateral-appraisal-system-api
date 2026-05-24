using System.Security.Claims;
using OpenIddict.Abstractions;

namespace Auth.Application.Features.Preferences.GetMyPreference;

public class GetMyPreferenceEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/me/preferences/{key}",
                async (string key, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
                {
                    var userIdClaim = user.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;
                    if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                        return Results.Unauthorized();

                    var result = await sender.Send(new GetMyPreferenceQuery(userId, key), cancellationToken);

                    if (result.Value is null)
                        return Results.NotFound();

                    return Results.Ok(new { value = result.Value });
                })
            .RequireAuthorization()
            .WithName("GetMyPreference")
            .WithSummary("Get a user preference by key")
            .WithDescription("Returns the stored JSON value for the given preference key. Returns 404 if not yet set.")
            .WithTags("Preferences");
    }
}
