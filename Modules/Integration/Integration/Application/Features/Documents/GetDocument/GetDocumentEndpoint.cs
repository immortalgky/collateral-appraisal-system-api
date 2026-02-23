using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.Documents.GetDocument;

public record GetDocumentResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string? DocumentType,
    string? Category,
    string DownloadUrl,
    DateTime CreatedOn
);

public class GetDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/documents/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new GetDocumentQuery(id);
            var result = await sender.Send(query, cancellationToken);

            return Results.Ok(new GetDocumentResponse(
                result.Id,
                result.FileName,
                result.ContentType,
                result.FileSizeBytes,
                result.DocumentType,
                result.Category,
                result.DownloadUrl,
                result.CreatedOn
            ));
        })
        .WithName("GetDocumentForIntegration")
        .WithTags("Integration - Documents")
        .Produces<GetDocumentResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
