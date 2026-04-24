namespace Appraisal.Application.Features.Quotations.SetSharedDocuments;

public class SetSharedDocumentsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/quotations/{id:guid}/shared-documents",
                async (
                    Guid id,
                    SetSharedDocumentsRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new SetSharedDocumentsCommand(id, request.Documents);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                })
            .WithName("SetQuotationSharedDocuments")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Set shared documents for a quotation")
            .WithDescription("Admin sets which appraisal/title-level documents are shared with invited companies. Full-replace semantics. Draft-only.")
            .WithTags("Quotation")
            .RequireAuthorization();
    }
}

public record SetSharedDocumentsRequest(IReadOnlyList<SharedDocumentEntry> Documents);
