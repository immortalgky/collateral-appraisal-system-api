namespace Parameter.DocumentRequirements.Features.DeleteDocumentRequirement;

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
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Document Requirements");
    }
}
