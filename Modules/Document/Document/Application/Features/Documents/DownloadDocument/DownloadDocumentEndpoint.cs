using Document.Services;
using Microsoft.AspNetCore.Mvc;

namespace Document.Domain.Documents.Features.DownloadDocument;

public class DownloadDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/documents/{id:guid}/download", async (
            Guid id,
            [FromQuery] bool download,
            [FromQuery] string? size,
            IImageResizeService imageResizeService,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            // Validate size parameter early
            if (size is not null && !imageResizeService.IsValidSize(size))
            {
                return Results.BadRequest($"Invalid size '{size}'. Valid sizes: small, medium, large.");
            }

            var query = new DownloadDocumentQuery(id, download);
            var result = await sender.Send(query, cancellationToken);

            if (!result.FileExists)
            {
                return Results.NotFound($"Document with ID {id} not found");
            }

            // Resize if size requested and file is an image
            if (size is not null && imageResizeService.IsImage(result.MimeType))
            {
                var resizedBytes = imageResizeService.Resize(result.FilePath, size);
                var mimeType = imageResizeService.GetResizedMimeType(result.MimeType);

                return Results.File(
                    resizedBytes,
                    contentType: mimeType,
                    fileDownloadName: download ? result.FileName : null
                );
            }

            // Original file path — stream as-is
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
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .AllowAnonymous();
    }
}
