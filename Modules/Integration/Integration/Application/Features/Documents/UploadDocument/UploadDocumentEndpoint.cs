using Carter;
using Document.Domain.Documents.Features.UploadDocument;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared.Configurations;

namespace Integration.Application.Features.Documents.UploadDocument;

public class UploadDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // See the Document module's UploadDocumentEndpoint: the server's default body cap sits
        // below MaxFileSizeBytes and has to be raised per endpoint.
        var maxRequestBodyBytes = app.ServiceProvider
            .GetRequiredService<IOptions<FileStorageConfiguration>>().Value.MaxRequestBodyBytes;

        app.MapPost("/api/v1/documents", async (
                HttpRequest request,
                ISender sender,
                IOptions<FileStorageConfiguration> fileStorageConfigurationOptions,
                CancellationToken cancellationToken) =>
            {
                var maxFileSize = fileStorageConfigurationOptions.Value.MaxFileSizeBytes;
                var maxRequestBody = fileStorageConfigurationOptions.Value.MaxRequestBodyBytes;

                // Prevent memory exhaustion. The body, not the file: a multipart request is a little
                // larger than what it carries. Plain text rather than the ProblemDetails the SPA's
                // route returns — this is the external route, LOS/CLS have been reading this shape
                // since it shipped, and the reason for the change (the SPA reads `apiError.detail`)
                // does not apply to a caller that never sees it.
                //
                // One case escapes this check and cannot be kept to 400: a body sent without a
                // Content-Length (chunked). The server stops it while reading and the shared
                // handler answers 413 ProblemDetails. A caller posting a file from disk always
                // sends a length, so this is an edge rather than the shape anyone integrates
                // against — but it is not 400, and pretending otherwise in this comment would be
                // worse than saying so.
                if (request.ContentLength > maxRequestBody)
                    return Results.BadRequest($"File too large. Maximum size is {maxFileSize / (1024 * 1024)}MB");

                var form = await request.ReadFormAsync(cancellationToken);

                // Prevent IndexOutOfRangeException
                if (form.Files.Count == 0)
                    return Results.BadRequest("No file uploaded");

                // Between the envelope limit above and the file limit lies a band the validator
                // would answer with its own raw text, leaking internal property paths to an
                // external caller. Refuse it here, in the same words as the check above.
                if (form.Files[0].Length > maxFileSize)
                    return Results.BadRequest($"File too large. Maximum size is {maxFileSize / (1024 * 1024)}MB");

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
            .WithMetadata(new RequestSizeLimitAttribute(maxRequestBodyBytes))
            .WithName("API - UploadDocument")
            .WithTags("Integration - Documents")
            .DisableAntiforgery()
            // 200, which is what the handler returns — the 201 this used to declare was never
            // true, and it is what an external integrator generates a client from.
            .Produces<UploadDocumentResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            // A body with no Content-Length is stopped by the server while it reads, which answers
            // 413 rather than the 400 above — rare from a caller posting a file, but real.
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}