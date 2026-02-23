using System.Security.Claims;
using OpenIddict.Abstractions;

namespace Auth.Domain.Auth.Features.Me;

public class MeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/me",
            async (ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            {
                var userIdClaim = user.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                    return Results.Unauthorized();

                var query = new MeQuery(userId);
                var result = await sender.Send(query, cancellationToken);
                var response = result.Adapt<MeResponse>();

                return Results.Ok(response);
            })
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .Produces<MeResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get current authenticated user")
            .WithDescription("Returns the current user's profile including roles and permissions")
            .WithTags("Auth");
    }
}
