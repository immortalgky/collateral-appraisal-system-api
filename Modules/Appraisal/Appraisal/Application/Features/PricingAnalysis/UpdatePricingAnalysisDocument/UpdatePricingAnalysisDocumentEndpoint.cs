namespace Appraisal.Application.Features.PricingAnalysis.UpdatePricingAnalysisDocument;

public class UpdatePricingAnalysisDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/pricing-analysis/{id:guid}/documents/{documentEntryId:guid}",
                async (
                    Guid id,
                    Guid documentEntryId,
                    UpdatePricingAnalysisDocumentRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new UpdatePricingAnalysisDocumentCommand(
                        id,
                        documentEntryId,
                        request.DocumentId,
                        request.FileName);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<UpdatePricingAnalysisDocumentResponse>();

                    return Results.Ok(response);
                })
            .WithName("UpdatePricingAnalysisDocument")
            .Produces<UpdatePricingAnalysisDocumentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Replace the document linked to a pricing analysis document entry")
            .WithDescription("Pass a new DocumentId to replace the linked file, or null to unlink without deleting the entry.")
            .WithTags("PricingAnalysis");
    }
}
