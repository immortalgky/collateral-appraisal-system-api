namespace Appraisal.Application.Features.MarketComparableTemplates.GetMarketComparableTemplates;

public class GetMarketComparableTemplatesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/market-comparable-templates",
                async ([AsParameters] GetMarketComparableTemplatesQueryParams queryParams,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var query = new GetMarketComparableTemplatesQuery(
                        queryParams.PropertyType,
                        queryParams.IsActive);

                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(new GetMarketComparableTemplatesResponse(result.Templates));
                })
            .WithName("GetMarketComparableTemplates")
            .Produces<GetMarketComparableTemplatesResponse>(StatusCodes.Status200OK)
            .WithSummary("Get market comparable templates")
            .WithDescription(
                "Retrieve all market comparable templates with optional filtering by property type and active status.")
            .WithTags("MarketComparableTemplates");
    }
}

public record GetMarketComparableTemplatesQueryParams(
    string? PropertyType = null,
    bool? IsActive = null
);