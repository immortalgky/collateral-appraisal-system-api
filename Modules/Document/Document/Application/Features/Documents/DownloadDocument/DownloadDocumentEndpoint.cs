using Microsoft.AspNetCore.Mvc;

namespace Document.Domain.Documents.Features.DownloadDocument;

public class DownloadDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/documents/{id:guid}/download", async (
            Guid id,
            [FromQuery] bool download,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new DownloadDocumentQuery(id, download);
            var result = await sender.Send(query, cancellationToken);

            if (!result.FileExists)
            {
                return Results.NotFound($"Document with ID {id} not found");
            }

            var fileStream = File.OpenRead(result.FilePath);

            return Results.File(
                fileStream,
                contentType: result.MimeType,
                fileDownloadName: download ? result.FileName : null,
                enableRangeProcessing: true
            );
        })
        .WithName("DownloadDocument")
        .WithTags("Documents")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .AllowAnonymous();
    }
}
