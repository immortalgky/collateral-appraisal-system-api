using System;

namespace Request.RequestDocuments.Features.AddRequestDocument;

public class AddRequestDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/requests/{requestId}/document",
                async (Guid requestId, AddRequestDocumentRequest request, ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new AddRequestDocumentCommand(
                        request.Documents.Select(d => d with { RequestId = requestId }).ToList()
                    );

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<AddRequestDocumentResponse>();

                    return Results.Ok(response);
                })
            .WithName("AddRequestDocument")
            .Produces<AddRequestDocumentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Add an document request")
            .WithDescription(
                "Add an document request in the system. The request details are provided in the request body.")
            .AllowAnonymous();
        // .RequireAuthorization("CanWriteRequest");
    }
}
