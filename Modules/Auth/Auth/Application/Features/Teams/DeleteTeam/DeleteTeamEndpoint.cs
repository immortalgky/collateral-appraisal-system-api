namespace Auth.Application.Features.Teams.DeleteTeam;

public class DeleteTeamEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/auth/teams/{id:guid}",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new DeleteTeamCommand(id);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                })
            .WithName("DeleteTeam")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete a team")
            .WithDescription("Hard-delete a team and all its members.")
            .WithTags("Team")
            .RequireAuthorization("CanManageTeams");
    }
}
