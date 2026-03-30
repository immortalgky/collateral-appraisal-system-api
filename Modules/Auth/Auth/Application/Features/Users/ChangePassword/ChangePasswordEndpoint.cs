using System.Security.Claims;
using OpenIddict.Abstractions;

namespace Auth.Application.Features.Users.ChangePassword;

public class ChangePasswordEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/auth/users/{id:guid}/change-password",
                async (Guid id, ChangePasswordRequest request, ClaimsPrincipal user,
                    ISender sender, CancellationToken cancellationToken) =>
                {
                    var callerIdClaim = user.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;
                    if (string.IsNullOrEmpty(callerIdClaim) || !Guid.TryParse(callerIdClaim, out var callerId))
                        return Results.Forbid();

                    var isSelf = callerId == id;

                    if (!isSelf && !user.HasClaim("permissions", "USER_CHANGE_PASSWORD"))
                        return Results.Forbid();

                    var command = new ChangePasswordCommand(id, request.CurrentPassword, request.NewPassword, request.ConfirmPassword);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                })
            .WithName("ChangePassword")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Change password")
            .WithDescription("Change password for a local auth user. Self or requires USER_CHANGE_PASSWORD permission.")
            .WithTags("User");
    }
}
