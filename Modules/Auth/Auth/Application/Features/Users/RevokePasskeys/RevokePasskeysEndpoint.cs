using Auth.Application.Services;

namespace Auth.Application.Features.Users.RevokePasskeys;

public class RevokePasskeysEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/auth/users/{id:guid}/passkeys",
                async (Guid id, ISender sender, IUserSessionService sessions, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new RevokePasskeysCommand(id), cancellationToken);

                    // The ended sessions' cached rows, dropped only now that the transaction has committed: dropped
                    // inside it, a concurrent request could read the row as live and cache it again.
                    sessions.Evict(result.EndedSessionIds);
                    return Results.NoContent();
                })
            .WithName("RevokePasskeys")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove all of a user's passkeys")
            .WithDescription("Removes every passkey the user registered and ends their sessions. Two-factor settings are left as they are.")
            .WithTags("User")
            .RequireAuthorization("CanManageUsers");
    }
}
