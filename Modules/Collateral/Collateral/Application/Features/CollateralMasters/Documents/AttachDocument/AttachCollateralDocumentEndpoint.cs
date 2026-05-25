namespace Collateral.Application.Features.CollateralMasters.Documents.AttachDocument;

/// <summary>
/// POST /collateral-masters/{id}/documents
/// Attaches a document to a CollateralMaster.
///
/// Two-step upload flow:
///   1. Upload the file: POST /api/v1/documents  → returns { documentId, fileName, ... }
///   2. Attach to master: POST /collateral-masters/{id}/documents  (this endpoint)
///      Body: { documentType, documentId, fileName, description? }
/// </summary>
public class AttachCollateralDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/collateral-masters/{id:guid}/documents",
                async (
                    Guid id,
                    AttachCollateralDocumentRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new AttachCollateralDocumentCommand(
                        id,
                        request.DocumentType,
                        request.DocumentId,
                        request.FileName,
                        request.Description);

                    var result = await sender.Send(command, cancellationToken);

                    return Results.Created(
                        $"/collateral-masters/{id}/documents/{result.DocumentRowId}",
                        new AttachCollateralDocumentResponse(result.DocumentRowId));
                })
            .WithName("AttachCollateralDocument")
            .Produces<AttachCollateralDocumentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Attach a document to a collateral master")
            .WithDescription(
                "Attaches a legal evidence document (title deed, lease contract, etc.) to a CollateralMaster. " +
                "Upload the file first via POST /api/v1/documents to obtain a DocumentId, then call this endpoint.")
            .WithTags("CollateralMaster")
            .RequireAuthorization();
    }
}

public record AttachCollateralDocumentRequest(
    string DocumentType,
    Guid DocumentId,
    string FileName,
    string? Description
);

public record AttachCollateralDocumentResponse(Guid DocumentRowId);
