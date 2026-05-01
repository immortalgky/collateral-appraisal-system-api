using Carter;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.DeleteHypothesisUnitDetailUpload;

public class DeleteHypothesisUnitDetailUploadEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/pricing-analysis/{pricingAnalysisId:guid}/methods/{methodId:guid}/hypothesis-analysis/uploads/{uploadId:guid}",
                async (Guid pricingAnalysisId, Guid methodId, Guid uploadId, ISender sender) =>
                {
                    var command = new DeleteHypothesisUnitDetailUploadCommand(
                        pricingAnalysisId, methodId, uploadId);
                    await sender.Send(command);
                    return Results.NoContent();
                })
            .WithName("DeleteHypothesisUnitDetailUpload")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete hypothesis unit detail upload")
            .WithDescription("Hard-deletes an upload and its unit rows. If it was active, no auto-promotion — user must re-upload.")
            .WithTags("PricingAnalysis");
    }
}
