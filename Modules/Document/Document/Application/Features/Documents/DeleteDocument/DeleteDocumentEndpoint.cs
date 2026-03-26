using Microsoft.AspNetCore.Mvc;

namespace Document.Domain.Documents.Features.DeleteDocument;

public class DeleteDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/documents/{id:guid}",
            async ([FromRoute] Guid id, [FromServices] ISender sender, CancellationToken cancellationToken) =>
            {
                var command = new DeleteDocumentCommand(id);

                var result = await sender.Send(command, cancellationToken);

                var response = result.Adapt<DeleteDocumentResponse>();

                return Results.Ok(response);
            });
        //.RequireAuthorization("CanWriteDocument");
    }
}