namespace Parameter.DocumentRequirements.Features.ReorderDocumentTypes;

public record ReorderDocumentTypesRequest(List<DocumentTypeOrderItem> Items);

public class ReorderDocumentTypesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/document-types/reorder", async (
                ReorderDocumentTypesRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new ReorderDocumentTypesCommand(request.Items);
                await sender.Send(command, cancellationToken);

                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithName("ReorderDocumentTypes")
            .WithSummary("Re-sequence document types by sort order")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Document Types");
    }
}
