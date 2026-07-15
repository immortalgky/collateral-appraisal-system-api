namespace Appraisal.Application.Features.PricingAnalysis.RemovePricingAnalysisDocument;

public class RemovePricingAnalysisDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/pricing-analysis/{id:guid}/documents/{documentEntryId:guid}",
                async (
                    Guid id,
                    Guid documentEntryId,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new RemovePricingAnalysisDocumentCommand(id, documentEntryId);
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("RemovePricingAnalysisDocument")
            .Produces<RemovePricingAnalysisDocumentResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove a document entry from a pricing analysis")
            .WithDescription("Deletes the PricingAnalysisDocument entry entirely (not just the file link).")
            .WithTags("PricingAnalysis");
    }
}
