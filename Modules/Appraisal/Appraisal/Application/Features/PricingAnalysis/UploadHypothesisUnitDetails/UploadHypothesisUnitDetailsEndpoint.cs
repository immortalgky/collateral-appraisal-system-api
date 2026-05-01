using Carter;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.UploadHypothesisUnitDetails;

public class UploadHypothesisUnitDetailsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/pricing-analysis/{pricingAnalysisId:guid}/methods/{methodId:guid}/hypothesis-analysis/uploads",
                async (
                    Guid pricingAnalysisId,
                    Guid methodId,
                    IFormFile file,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var extension = Path.GetExtension(file.FileName);
                    if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
                    {
                        return Results.Problem(
                            title: "Invalid file type",
                            detail: "Only .xlsx files are allowed.",
                            statusCode: StatusCodes.Status400BadRequest);
                    }

                    const long maxFileSize = 5 * 1024 * 1024;
                    if (file.Length > maxFileSize)
                    {
                        return Results.Problem(
                            title: "File too large",
                            detail: "File size must not exceed 5 MB.",
                            statusCode: StatusCodes.Status400BadRequest);
                    }

                    using var stream = file.OpenReadStream();
                    var command = new UploadHypothesisUnitDetailsCommand(
                        pricingAnalysisId, methodId, file.FileName, stream);

                    var result = await sender.Send(command, cancellationToken);
                    return Results.Created(
                        $"/pricing-analysis/{pricingAnalysisId}/methods/{methodId}/hypothesis-analysis/uploads/{result.UploadId}",
                        result);
                })
            .WithName("UploadHypothesisUnitDetails")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<UploadHypothesisUnitDetailsResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Upload hypothesis unit details")
            .WithDescription("Upload Excel file with unit details for hypothesis pricing analysis. Deactivates any previous upload.")
            .WithTags("PricingAnalysis")
            .DisableAntiforgery();
    }
}
