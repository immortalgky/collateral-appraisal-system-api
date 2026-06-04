namespace Auth.Application.Features.Users.UpdateUserTeams;

public class UpdateUserTeamsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/auth/users/{id:guid}/teams",
                async (Guid id, UpdateUserTeamsRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new UpdateUserTeamsCommand(id, request.TeamIds);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                })
            .WithName("UpdateUserTeams")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update user teams (admin)")
            .WithDescription("Replace all team memberships for a user.")
            .WithTags("User")
            .RequireAuthorization("CanManageUsers");
    }
}
