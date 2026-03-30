namespace Request.Application.Features.Requests.AttachRequestDocument;

public class AttachRequestDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/requests/{requestId:guid}/documents", async (
                Guid requestId,
                AttachRequestDocumentRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new AttachRequestDocumentCommand(
                    RequestId: requestId,
                    DocumentId: request.DocumentId,
                    DocumentType: request.DocumentType,
                    FileName: request.FileName,
                    Source: request.Source);

                var result = await sender.Send(command, cancellationToken);

                return Results.Ok(new AttachRequestDocumentResponse(result.IsSuccess));
            })
            .WithName("AttachRequestDocument")
            .WithSummary("Attach a document to a request")
            .WithDescription("Links an already-uploaded document to a request.")
            .Produces<AttachRequestDocumentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Requests");
    }
}
