namespace Auth.Application.Features.Companies.GetCompanies;

public class GetCompaniesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/companies", async (
                [AsParameters] GetCompaniesQuery query,
                ISender sender) =>
            {
                var result = await sender.Send(query);
                var response = result.Adapt<GetCompaniesResponse>();
                return Results.Ok(response);
            })
            .WithName("GetCompanies")
            .Produces<GetCompaniesResponse>()
            .WithSummary("Get Companies")
            .WithDescription("Get all companies with optional search and active filter");
    }
}
