namespace Auth.Application.Features.Teams.GetTeamById;

public class GetTeamByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/auth/teams/{id:guid}",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetTeamByIdQuery(id);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetTeamById")
            .Produces<GetTeamByIdResult>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get team by ID")
            .WithDescription("Get team detail including member user IDs.")
            .WithTags("Team");
    }
}
