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
                    HttpResponse response,
                    CancellationToken cancellationToken) =>
                {
                    var command = new SetAccessWindowCommand(
                        id, request.ExpiresAt, request.Reason, request.ExtendOnly);
                    var result = await sender.Send(command, cancellationToken);

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
