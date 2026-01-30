namespace Appraisal.Application.Features.DocumentRequirements.DeleteDocumentType;

/// <summary>
/// Endpoint: DELETE /document-types/{id}
/// Soft deletes (deactivates) a document type
/// </summary>
public class DeleteDocumentTypeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/document-types/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new DeleteDocumentTypeCommand(id);
                await sender.Send(command, cancellationToken);

                return Results.NoContent();
            })
            .WithName("DeleteDocumentType")
            .WithSummary("Delete a document type")
            .WithDescription("Soft deletes (deactivates) a document type")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Document Types");
    }
}
