namespace Appraisal.Application.Features.DocumentRequirements.CreateDocumentRequirement;

/// <summary>
/// Endpoint: POST /document-requirements
/// Creates a new document requirement
/// </summary>
public class CreateDocumentRequirementEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/document-requirements", async (
                CreateDocumentRequirementRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new CreateDocumentRequirementCommand(
                    request.DocumentTypeId,
                    request.PropertyTypeCode,
                    request.PurposeCode,
                    request.IsRequired,
                    request.Notes);

                var result = await sender.Send(command, cancellationToken);

                return Results.Created(
                    $"/document-requirements/{result.Id}",
                    new CreateDocumentRequirementResponse(result.Id));
            })
            .WithName("CreateDocumentRequirement")
            .WithSummary("Create a document requirement")
            .WithDescription("Creates a new document requirement. Use null PropertyTypeCode for application-level, null PurposeCode for purpose-agnostic requirements.")
            .Produces<CreateDocumentRequirementResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Document Requirements");
    }
}
