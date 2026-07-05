namespace Appraisal.Application.Features.PricingAnalysis.AttachPricingAnalysisDocument;

public class AttachPricingAnalysisDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/pricing-analysis/{id:guid}/documents",
                async (
                    Guid id,
                    AttachPricingAnalysisDocumentRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new AttachPricingAnalysisDocumentCommand(
                        id,
                        request.DocumentId,
                        request.FileName);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<AttachPricingAnalysisDocumentResponse>();

                    return Results.Ok(response);
                })
            .WithName("AttachPricingAnalysisDocument")
            .Produces<AttachPricingAnalysisDocumentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Attach a document to a pricing analysis")
            .WithDescription("Links an already-uploaded document (via POST /documents) to a pricing analysis.")
            .WithTags("PricingAnalysis");
    }
}
