namespace Appraisal.Application.Features.DocumentRequirements.UpdateDocumentRequirement;

/// <summary>
/// Endpoint: PUT /document-requirements/{id}
/// Updates an existing document requirement
/// </summary>
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
            .WithDescription("Updates an existing document requirement's isRequired, isActive, and notes fields")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Document Requirements");
    }
}
