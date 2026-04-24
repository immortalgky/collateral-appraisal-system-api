namespace Appraisal.Application.Features.Quotations.StreamSharedDocument;

/// <summary>
/// v7: inline-view endpoint for a document that admin explicitly shared on a quotation.
/// Authorization is enforced by <see cref="StreamSharedDocumentQueryHandler"/> via
/// <c>DocumentAccessPolicy</c>. Streams with Content-Disposition: inline — no download.
/// </summary>
public class StreamSharedDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/quotations/{id:guid}/shared-documents/{documentId:guid}/content",
                async (
                    Guid id,
                    Guid documentId,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var query = new StreamSharedDocumentQuery(id, documentId);
                    var result = await sender.Send(query, cancellationToken);

                    // Open as a read-only stream; enable range so inline PDF viewers can seek.
                    var stream = File.OpenRead(result.FilePath);

                    // fileDownloadName = null ⇒ inline (no Content-Disposition: attachment header).
                    return Results.File(
                        stream,
                        contentType: result.MimeType,
                        fileDownloadName: null,
                        enableRangeProcessing: true);
                })
            .WithName("StreamQuotationSharedDocument")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Stream a shared quotation document inline")
            .WithDescription(
                "Streams a document that an admin has explicitly shared for the given quotation. " +
                "Authorized to: admin, the RM who owns the linked request, invited external company users (active " +
                "invitation only), or the post-finalize winning company for as long as their assignment is active.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}
