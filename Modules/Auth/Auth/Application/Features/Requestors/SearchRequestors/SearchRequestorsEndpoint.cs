namespace Auth.Application.Features.Requestors.SearchRequestors;

public class SearchRequestorsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/auth/requestors",
                async (
                    string? search,
                    int pageNumber = 1,
                    int pageSize = 20,
                    ISender sender = default!,
                    CancellationToken cancellationToken = default) =>
                {
                    var query = new SearchRequestorsQuery(search, pageNumber, pageSize);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .RequireAuthorization()
            .WithName("SearchRequestors")
            .Produces<SearchRequestorsResult>()
            .WithSummary("Search requestors")
            .WithDescription("Search active users by employee ID, name, or email to use as the requestor on a new appraisal request.")
            .WithTags("Requestors");
    }
}
