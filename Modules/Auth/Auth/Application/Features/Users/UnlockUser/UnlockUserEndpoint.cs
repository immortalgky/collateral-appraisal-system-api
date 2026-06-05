namespace Auth.Application.Features.Users.UnlockUser;

public class UnlockUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/auth/users/{id:guid}/unlock",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new UnlockUserCommand(id);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                })
            .WithName("UnlockUser")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Unlock a user account")
            .WithDescription("Clear the lockout end date and reset the failed access count.")
            .WithTags("User")
            .RequireAuthorization("CanManageUsers");
    }
}
