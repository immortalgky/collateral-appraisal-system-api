namespace Auth.Application.Features.Teams.GetTeams;

public class GetTeamsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/auth/teams",
                async (
                    string? search,
                    string? type,
                    int pageNumber = 1,
                    int pageSize = 20,
                    ISender sender = default!,
                    CancellationToken cancellationToken = default) =>
                {
                    var query = new GetTeamsQuery(search, type, pageNumber, pageSize);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetTeamsResponse>();
                    return Results.Ok(response);
                })
            .WithName("GetTeams")
            .Produces<GetTeamsResponse>()
            .WithSummary("Get teams")
            .WithDescription("Get a paginated list of teams, optionally filtered by type (Internal/External).")
            .WithTags("Team");
    }
}
