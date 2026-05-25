using System.Security.Claims;
using System.Text.Json;
using OpenIddict.Abstractions;

namespace Auth.Application.Features.Preferences.SetMyPreference;

public record SetMyPreferenceRequest(JsonElement Value);

public class SetMyPreferenceEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/auth/me/preferences/{key}",
                async (string key, SetMyPreferenceRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
                {
                    var userIdClaim = user.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;
                    if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                        return Results.Unauthorized();

                    await sender.Send(new SetMyPreferenceCommand(userId, key, request.Value), cancellationToken);
                    return Results.NoContent();
                })
            .RequireAuthorization()
            .WithName("SetMyPreference")
            .WithSummary("Set a user preference by key")
            .WithDescription("Upserts the JSON value for the given preference key. Key must be in the allowlist.")
            .WithTags("Preferences");
    }
}
