namespace Parameter.DocumentRequirements.Features.CreateDocumentRequirement;

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
            .Produces<CreateDocumentRequirementResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Document Requirements");
    }
}
