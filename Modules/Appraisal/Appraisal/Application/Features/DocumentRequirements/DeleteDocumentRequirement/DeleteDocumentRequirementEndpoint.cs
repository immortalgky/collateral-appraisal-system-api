namespace Appraisal.Application.Features.DocumentRequirements.DeleteDocumentRequirement;

/// <summary>
/// Endpoint: DELETE /document-requirements/{id}
/// Soft deletes (deactivates) a document requirement
/// </summary>
public class DeleteDocumentRequirementEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/document-requirements/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new DeleteDocumentRequirementCommand(id);
                await sender.Send(command, cancellationToken);

                return Results.NoContent();
            })
            .WithName("DeleteDocumentRequirement")
            .WithSummary("Delete a document requirement")
            .WithDescription("Soft deletes (deactivates) a document requirement")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Document Requirements");
    }
}
