namespace Parameter.DocumentRequirements.Features.CreateDocumentType;

public record CreateDocumentTypeRequest(
    string Code,
    string Name,
    string? Description,
    string? Category,
    int SortOrder = 0);

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

                var response = result.Adapt<CreateDocumentTypeResponse>();
                return Results.Created($"/document-types/{response.Id}", response);
            })
            .WithName("CreateDocumentType")
            .WithSummary("Create a new document type")
            .Produces<CreateDocumentTypeResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Document Types");
    }
}
