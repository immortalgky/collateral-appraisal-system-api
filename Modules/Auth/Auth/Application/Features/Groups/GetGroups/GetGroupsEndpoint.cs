namespace Auth.Application.Features.Groups.GetGroups;

public class GetGroupsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/auth/groups",
                async (
                    string? search,
                    string? scope,
                    int pageNumber = 1,
                    int pageSize = 20,
                    ISender sender = default!,
                    CancellationToken cancellationToken = default) =>
                {
                    var query = new GetGroupsQuery(search, scope, pageNumber, pageSize);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetGroupsResponse>();
                    return Results.Ok(response);
                })
            .WithName("GetGroups")
            .Produces<GetGroupsResponse>()
            .WithSummary("Get groups")
            .WithDescription("Get a paginated list of groups, optionally filtered by scope (Bank/Company).")
            .WithTags("Group");
    }
}
