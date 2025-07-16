using Microsoft.AspNetCore.Mvc;

namespace Document.Documents.Features.DeleteDocument;

public class DeleteDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/documents/{id:long}",
            async ([FromRoute]long id,[FromServices]ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new DeleteDocumentCommand(id);

            var result = await sender.Send(command, cancellationToken);

            var response = result.Adapt<DeleteDocumentResponse>();

            return Results.Ok(response);
        });
    }
}