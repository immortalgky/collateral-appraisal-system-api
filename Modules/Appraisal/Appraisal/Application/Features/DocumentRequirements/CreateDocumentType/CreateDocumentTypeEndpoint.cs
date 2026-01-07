namespace Appraisal.Application.Features.DocumentRequirements.CreateDocumentType;

/// <summary>
/// Request body for creating a document type
/// </summary>
public record CreateDocumentTypeRequest(
    string Code,
    string Name,
    string? Description,
    string? Category,
    int SortOrder = 0);

/// <summary>
/// Endpoint: POST /document-types
/// Creates a new document type
/// </summary>
public class CreateDocumentTypeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/document-types", async (
                CreateDocumentTypeRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new CreateDocumentTypeCommand(
                    request.Code,
                    request.Name,
                    request.Description,
                    request.Category,
                    request.SortOrder);

                var result = await sender.Send(command, cancellationToken);

                return Results.Created($"/document-types/{result.Id}", result);
            })
            .WithName("CreateDocumentType")
            .WithSummary("Create a new document type")
            .WithDescription("Creates a new document type for use in document requirements")
            .Produces<CreateDocumentTypeResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Document Types");
    }
}
