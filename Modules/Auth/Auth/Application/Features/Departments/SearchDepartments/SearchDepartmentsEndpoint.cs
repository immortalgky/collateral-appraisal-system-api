namespace Auth.Application.Features.Departments.SearchDepartments;

public class SearchDepartmentsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/auth/departments",
                async (
                    string? search,
                    int pageSize = 50,
                    ISender sender = default!,
                    CancellationToken cancellationToken = default) =>
                {
                    var query = new SearchDepartmentsQuery(search, pageSize);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .RequireAuthorization()
            .WithName("SearchDepartments")
            .Produces<SearchDepartmentsResult>()
            .WithSummary("Search departments")
            .WithDescription("Active department reference data (AS400 code + description), for department filters.")
            .WithTags("Departments");
    }
}
