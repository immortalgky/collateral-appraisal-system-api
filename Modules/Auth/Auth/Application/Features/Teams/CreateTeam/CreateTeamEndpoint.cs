namespace Auth.Application.Features.Teams.CreateTeam;

public class CreateTeamEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/auth/teams",
                async (CreateTeamRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new CreateTeamCommand(request.Name, request.Scope, request.Description);
                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<CreateTeamResponse>();
                    return Results.Created($"/auth/teams/{response.Id}", response);
                })
            .WithName("CreateTeam")
            .Produces<CreateTeamResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Create a new team")
            .WithDescription("Create a new Bank or Company team.")
            .WithTags("Team")
            .RequireAuthorization("CanManageTeams");
    }
}
