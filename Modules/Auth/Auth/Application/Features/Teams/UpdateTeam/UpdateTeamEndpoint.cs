namespace Auth.Application.Features.Teams.UpdateTeam;

public class UpdateTeamEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/auth/teams/{id:guid}",
                async (Guid id, UpdateTeamRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new UpdateTeamCommand(id, request.Name, request.Scope, request.Description);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                })
            .WithName("UpdateTeam")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update a team")
            .WithDescription("Update the name, scope, description, and active status of a team.")
            .WithTags("Team")
            .RequireAuthorization("CanManageTeams");
    }
}
