namespace Appraisal.Application.Features.PricingAnalysis.GetPricingAnalysisDocuments;

public class GetPricingAnalysisDocumentsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/pricing-analysis/{id:guid}/documents",
                async (
                    Guid id,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var query = new GetPricingAnalysisDocumentsQuery(id);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetPricingAnalysisDocumentsResponse>();

                    return Results.Ok(response);
                })
            .WithName("GetPricingAnalysisDocuments")
            .Produces<GetPricingAnalysisDocumentsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("List documents attached to a pricing analysis")
            .WithTags("PricingAnalysis");
    }
}
