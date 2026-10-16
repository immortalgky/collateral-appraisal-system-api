using Auth.Application.Services;

namespace Auth.Application.Features.Users.ResetTwoFactor;

public class ResetTwoFactorEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/auth/users/{id:guid}/two-factor/reset",
                async (Guid id, ISender sender, IUserSessionService sessions, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new ResetTwoFactorCommand(id), cancellationToken);

                    // The ended sessions' cached rows, dropped only now that the transaction has committed: dropped
                    // inside it, a concurrent request could read the row as live and cache it again.
                    sessions.Evict(result.EndedSessionIds);
                    return Results.NoContent();
                })
            .WithName("ResetTwoFactor")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Reset a user's two-factor sign-in")
            .WithDescription("Removes the authenticator key and the recovery codes, turns two-factor off and ends the user's sessions. The user can set it up again from their profile.")
            .WithTags("User")
            .RequireAuthorization("CanManageUsers");
    }
}
