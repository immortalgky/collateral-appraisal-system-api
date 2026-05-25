namespace Collateral.Application.Features.CollateralMasters.Documents.ArchiveDocument;

/// <summary>
/// DELETE /collateral-masters/{id}/documents/{documentRowId}
/// Soft-archives a CollateralDocument row (IsActive = 0). The row is not removed.
///
/// IMPORTANT: {documentRowId} is the PK of the CollateralDocument row (CollateralDocument.Id),
/// NOT the upstream Document module's DocumentId.
/// </summary>
public class ArchiveCollateralDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/collateral-masters/{id:guid}/documents/{documentRowId:guid}",
                async (
                    Guid id,
                    Guid documentRowId,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new ArchiveCollateralDocumentCommand(id, documentRowId);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                })
            .WithName("ArchiveCollateralDocument")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Soft-archive a document attached to a collateral master")
            .WithDescription(
                "Sets IsActive = false on the specified CollateralDocument row. " +
                "The row is NOT deleted — the DocumentId FK to the Document module remains intact. " +
                "The {documentRowId} path parameter is the CollateralDocument row PK, " +
                "not the upstream Document module's DocumentId.")
            .WithTags("CollateralMaster")
            .RequireAuthorization();
    }
}
