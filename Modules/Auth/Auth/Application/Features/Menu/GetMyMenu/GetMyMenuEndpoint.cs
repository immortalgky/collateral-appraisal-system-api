using System.Security.Claims;
using OpenIddict.Abstractions;

namespace Auth.Application.Features.Menu.GetMyMenu;

public class GetMyMenuEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/me/menu",
                async (
                    ClaimsPrincipal user,
                    ISender sender,
                    CancellationToken cancellationToken,
                    [FromQuery(Name = "activityId")] string? activityId = null) =>
                {
                    var userIdClaim = user.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;
                    if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                        return Results.Unauthorized();

                    var normalizedActivityId = string.IsNullOrWhiteSpace(activityId) ? null : activityId.Trim();
                    var result = await sender.Send(new GetMyMenuQuery(userId, normalizedActivityId), cancellationToken);
                    return Results.Ok(result);
                })
            .RequireAuthorization()
            .WithName("GetMyMenu")
            .Produces<MyMenuResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get current user's navigation menu")
            .WithDescription("Returns the authenticated user's filtered menu tree (Main + Appraisal scopes) based on their permissions.")
            .WithTags("Menu");
    }
}
