namespace Parameter.DocumentRequirements.Features.UpdateDocumentRequirement;

public record UpdateDocumentRequirementRequest(
    bool IsRequired,
    bool IsActive,
    string? Notes);

public class UpdateDocumentRequirementEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/document-requirements/{id:guid}", async (
                Guid id,
                UpdateDocumentRequirementRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdateDocumentRequirementCommand(
                    id,
                    request.IsRequired,
                    request.IsActive,
                    request.Notes);

                await sender.Send(command, cancellationToken);

                return Results.NoContent();
            })
            .WithName("UpdateDocumentRequirement")
            .WithSummary("Update a document requirement")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Document Requirements");
    }
}
