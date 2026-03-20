namespace Parameter.DocumentRequirements.Features.UpdateDocumentType;

public record UpdateDocumentTypeRequest(
    string Name,
    string? Description,
    string? Category,
    int SortOrder,
    bool IsActive);

public class UpdateDocumentTypeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/document-types/{id:guid}", async (
                Guid id,
                UpdateDocumentTypeRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdateDocumentTypeCommand(
                    id,
                    request.Name,
                    request.Description,
                    request.Category,
                    request.SortOrder,
                    request.IsActive);

                await sender.Send(command, cancellationToken);

                return Results.NoContent();
            })
            .WithName("UpdateDocumentType")
            .WithSummary("Update a document type")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Document Types");
    }
}
