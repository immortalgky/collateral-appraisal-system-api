using Carter;
using Document.Domain.Documents.Features.UploadDocument;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Shared.Configurations;

namespace Integration.Application.Features.Documents.UploadDocument;

public class UploadDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/documents", async (
                HttpRequest request,
                ISender sender,
                IOptions<FileStorageConfiguration> fileStorageConfigurationOptions,
                CancellationToken cancellationToken) =>
            {
                var maxFileSize = fileStorageConfigurationOptions.Value.MaxFileSizeBytes;

                // Prevent memory exhaustion
                if (request.ContentLength > maxFileSize)
                    return Results.BadRequest($"File too large. Maximum size is {maxFileSize / 1_000_000}MB");

                var form = await request.ReadFormAsync(cancellationToken);

                // Prevent IndexOutOfRangeException
                if (form.Files.Count == 0)
                    return Results.BadRequest("No file uploaded");

                // Prevent FormatException
                if (!Guid.TryParse(form["uploadSessionId"], out var uploadSessionId))
                    return Results.BadRequest("Invalid or missing uploadSessionId");

                var documentType = form["documentType"].ToString();
                var notes = form["notes"].ToString();

                if (string.IsNullOrWhiteSpace(documentType))
                    return Results.BadRequest("documentType is required");

                var command = new UploadDocumentCommand(
                    form.Files[0],
                    uploadSessionId,
                    documentType,
                    "",
                    notes
                );

                var result = await sender.Send(command, cancellationToken);

                var response = new UploadDocumentResponse(result.DocumentId, result.FileName, form.Files[0].ContentType,
                    result.FileSize);

                return Results.Ok(response);
            })
            .WithName("API - UploadDocument")
            .WithTags("Integration - Documents")
            .DisableAntiforgery()
            .Produces<UploadDocumentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}