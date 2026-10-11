using Auth.Application.Services;

namespace Auth.Application.Features.Users.SetAccessWindow;

public class SetAccessWindowEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/auth/users/{id:guid}/access-window",
                async (
                    Guid id,
                    SetAccessWindowRequest request,
                    ISender sender,
                    AuthorizationClaimsBuilder claims,
                    IUserSessionService sessions,
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                {
                    var command = new SetAccessWindowCommand(
                        id, request.ExpiresAt, request.Reason, request.ExtendOnly);
                    var result = await sender.Send(command, cancellationToken);

                    // Extending or closing the window must reach this node's cached view of the user now, not
                    // after its 30 s — and only once the transaction has committed: dropped inside the handler,
                    // a concurrent request could re-read the old window and cache it again. (The other node
                    // catches up within its cache lifetimes: 30 s for the user, 10 s for the sessions. It never ends a
                    // session on a cached copy alone, so an extension is not cut off late.)
                    claims.Invalidate(id);

                    // Same for the ended sessions' cached rows: evicted inside the transaction, a concurrent request
                    // under RCSI could still read the row as live and cache it again for 10 s.
                    sessions.Evict(result.EndedSessionIds);

                    // The response body can carry a live password. Keep it out of any shared cache
                    // and out of the browser's back/forward cache.
                    response.Headers.CacheControl = "no-store";
                    return Results.Ok(result);
                })
            .WithName("SetAccessWindow")
            .Produces<SetAccessWindowResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Open, extend or close a temporary account's access window")
            .WithDescription(
                "Opening returns a freshly generated password, shown once and never stored in readable "
                + "form. Extending keeps the current password. Sending an expiry in the past closes the "
                + "window and rotates the password so nothing usable is left behind.")
            .WithTags("User")
            .RequireAuthorization("CanManageUsers");
    }
}
