namespace Auth.Application.Features.Teams.UpdateTeamUsers;

public class UpdateTeamUsersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/auth/teams/{id:guid}/members",
                async (Guid id, UpdateTeamUsersRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new UpdateTeamUsersCommand(id, request.UserIds);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                })
            .WithName("UpdateTeamMembers")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update team members")
            .WithDescription("Replace all members of a team.")
            .WithTags("Team")
            .RequireAuthorization("CanManageTeams");
    }
}
