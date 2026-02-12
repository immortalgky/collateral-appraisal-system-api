using System.Security.Claims;
using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenIddict.Abstractions;

namespace Auth.Domain.Auth.Features.UpdateProfile;

public class UpdateProfileEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/auth/profile",
            async (
                UpdateProfileRequest request,
                ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var userIdClaim = user.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                    return Results.Unauthorized();

                var command = new UpdateProfileCommand(
                    userId,
                    request.FirstName,
                    request.LastName,
                    request.AvatarUrl,
                    request.Position,
                    request.Department
                );

                var result = await sender.Send(command, cancellationToken);
                var response = result.Adapt<UpdateProfileResponse>();

                return Results.Ok(response);
            })
            .RequireAuthorization()
            .WithName("UpdateProfile")
            .Produces<UpdateProfileResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update current user profile")
            .WithDescription("Updates the profile information of the currently authenticated user")
            .WithTags("Auth");
    }
}
