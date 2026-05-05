using Carter;
using Document.Domain.Documents.Features.DownloadDocument;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.Documents.DownloadDocument;

public class DownloadDocumentIntegrationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/documents/{id:guid}/download", async (
            Guid id,
            [Microsoft.AspNetCore.Mvc.FromQuery] bool download,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new DownloadDocumentQuery(id, download), cancellationToken);

            if (!result.FileExists)
                return Results.NotFound($"Document {id} not found");

            Stream fileStream;
            try { fileStream = File.OpenRead(result.FilePath); }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            { return Results.NotFound($"Document file for {id} not found"); }

            return Results.File(
                fileStream,
                contentType: result.MimeType,
                fileDownloadName: download ? result.FileName : null,
                enableRangeProcessing: true);
        })
        .WithName("DownloadDocumentIntegration")
        .WithTags("Integration - Documents")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization("Integration");
    }
}
