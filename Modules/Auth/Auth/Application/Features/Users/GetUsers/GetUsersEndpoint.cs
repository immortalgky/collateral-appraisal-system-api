namespace Auth.Application.Features.Users.GetUsers;

public class GetUsersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/auth/users",
                async (
                    string? search,
                    string? scope,
                    int pageNumber = 1,
                    int pageSize = 20,
                    ISender sender = default!,
                    CancellationToken cancellationToken = default) =>
                {
                    var query = new GetUsersQuery(search, scope, pageNumber, pageSize);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetUsersResponse>();
                    return Results.Ok(response);
                })
            .WithName("GetUsers")
            .Produces<GetUsersResponse>()
            .WithSummary("Get users (admin)")
            .WithDescription("Get a paginated list of users. Optionally filter by scope (Bank/Company).")
            .WithTags("User");
    }
}
