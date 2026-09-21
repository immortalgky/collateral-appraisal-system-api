using Carter;
using Document.Domain.Documents.Features.StagedUpload;
using Document.Services;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Shared.Configurations;

namespace Integration.Application.Features.Documents.UploadDocument;

/// <summary>
/// The upload route an outside system posts to. Unlike the app, LOS sends whatever it has in a
/// single request — it cannot split a file into chunks — so this route reads the body as it
/// arrives and writes it straight to storage.
/// </summary>
public class UploadDocumentEndpoint : ICarterModule
{
    /// <summary>
    /// Enough for the form fields that travel with the file. Enforced as a bound while reading
    /// rather than checked afterwards: a section is not obliged to be small, and buffering one to
    /// find out how big it is would be the very thing this route avoids doing with the file.
    /// </summary>
    private const int MaxFieldLength = 4096;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var configuration = app.ServiceProvider
            .GetRequiredService<IOptions<FileStorageConfiguration>>().Value;

        app.MapPost("/api/v1/documents", async (
                HttpRequest request,
                IDocumentService documentService,
                IOptions<FileStorageConfiguration> fileStorageOptions,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var maxFileSize = fileStorageOptions.Value.IntegrationMaxFileSizeBytes;

                // Kept from the version this replaces: a caller that declares an oversized body is
                // turned away here, cheaply, with the bare-string 400 that LOS and CLS have been
                // reading since this endpoint shipped. A body that understates its length, or sends
                // none at all, is stopped further in — by the server as a 413, or once the bytes
                // overrun the declared maximum.
                if (request.ContentLength > fileStorageOptions.Value.IntegrationMaxRequestBodyBytes)
                    return Results.BadRequest($"File too large. Maximum size is {maxFileSize / (1024 * 1024)}MB");

                var boundary = GetBoundary(request.ContentType);
                if (boundary is null)
                    return Results.BadRequest("Expected a multipart/form-data request.");

                // MultipartReader, not ReadFormAsync: the framework's form reader spools the whole
                // body to a temporary file on the system drive before the handler sees any of it,
                // and it caps a multipart body at 128 MB. Reading the sections as they arrive costs
                // one buffer, whatever the size of the file.
                var reader = new MultipartReader(boundary, request.Body);
                var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                StagedUpload? staged = null;
                string? fileName = null;
                string? contentType = null;

                IResult Refuse(string detail)
                {
                    // Every way out of here that is not the happy path has to take the staged bytes
                    // with it. Left behind, a gigabyte sits on the share until the next sweep a day
                    // later — and a caller looping a malformed request would fill it.
                    if (staged is not null) TryDelete(staged.StagedPath);
                    return Results.BadRequest(detail);
                }

                try
                {
                    for (var section = await reader.ReadNextSectionAsync(cancellationToken);
                         section is not null;
                         section = await reader.ReadNextSectionAsync(cancellationToken))
                    {
                        if (!ContentDispositionHeaderValue.TryParse(
                                section.ContentDisposition, out var disposition))
                            continue;

                        // A part carrying a filename parameter is the file, even when that
                        // parameter is blank: .NET reports `filename=""` as a *form* disposition,
                        // and handling it as one would read the whole upload into a string.
                        var declaresFile = disposition.FileName.HasValue || disposition.FileNameStar.HasValue;

                        if (declaresFile || disposition.IsFileDisposition())
                        {
                            if (staged is not null)
                                return Refuse("Only one file may be uploaded per request.");

                            fileName = disposition.FileName.Value ?? disposition.FileNameStar.Value;
                            if (string.IsNullOrWhiteSpace(fileName))
                                return Refuse("The file part has no filename.");

                            if (!IsAllowedExtension(fileName, fileStorageOptions.Value))
                                return Refuse(
                                    "File extension is not allowed. Allowed extensions: "
                                    + string.Join(", ", fileStorageOptions.Value.AllowedExtensions));

                            contentType = string.IsNullOrWhiteSpace(section.ContentType)
                                ? "application/octet-stream"
                                : section.ContentType;

                            // The file is written before the fields that describe it are
                            // necessarily known — a multipart body may order its parts however it
                            // likes, and this one cannot be rewound.
                            staged = await documentService.StageStreamAsync(
                                section.Body, fileName!, maxFileSize, cancellationToken);
                        }
                        else if (disposition.IsFormDisposition())
                        {
                            var name = disposition.Name.Value;
                            if (string.IsNullOrEmpty(name)) continue;

                            var value = await ReadFieldAsync(section.Body, cancellationToken);
                            if (value is null)
                                return Refuse($"The '{name}' field is too long.");

                            fields[name] = value;
                        }
                    }

                    if (staged is null)
                        return Refuse("No file uploaded");

                    if (staged.Length == 0)
                        return Refuse("File cannot be empty");

                    if (!Guid.TryParse(Field(fields, "uploadSessionId"), out var uploadSessionId))
                        return Refuse("Invalid or missing uploadSessionId");

                    // Handed to the command pipeline now that the bytes are down, so the row is
                    // written and committed the way every other write in the system is — and the
                    // command's validator answers for the field lengths before any of that runs.
                    var result = await sender.Send(
                        new CompleteStagedUploadCommand(
                            staged.DocumentId,
                            staged.StagedPath,
                            new StagedFileMetadata(
                                uploadSessionId,
                                fileName!,
                                staged.Length,
                                contentType!,
                                Field(fields, "documentType"),
                                string.Empty,
                                Field(fields, "notes")),
                            staged.ChecksumBase64),
                        cancellationToken);

                    return Results.Ok(new UploadDocumentResponse(
                        result.DocumentId, result.FileName, result.MimeType, result.FileSize));
                }
                catch
                {
                    if (staged is not null) TryDelete(staged.StagedPath);
                    throw;
                }
            })
            .WithMetadata(new RequestSizeLimitAttribute(configuration.IntegrationMaxRequestBodyBytes))
            .WithName("API - UploadDocument")
            .WithTags("Integration - Documents")
            .DisableAntiforgery()
            // 200, which is what this returns — the 201 it used to declare was never true, and it
            // is what an external integrator generates a client from.
            .Produces<UploadDocumentResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    /// <summary>
    /// Reads a form field, refusing (null) anything longer than <see cref="MaxFieldLength"/>
    /// without ever holding more than that in memory.
    /// </summary>
    private static async Task<string?> ReadFieldAsync(Stream body, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(body, leaveOpen: true);

        var buffer = new char[MaxFieldLength + 1];
        var read = await reader.ReadBlockAsync(buffer, cancellationToken);

        return read > MaxFieldLength ? null : new string(buffer, 0, read);
    }

    private static string? GetBoundary(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType)
            || !MediaTypeHeaderValue.TryParse(contentType, out var mediaType)
            || !mediaType.MediaType.HasValue
            || !mediaType.MediaType.Value!.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
            return null;

        var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;
        return string.IsNullOrWhiteSpace(boundary) ? null : boundary;
    }

    private static bool IsAllowedExtension(string fileName, FileStorageConfiguration configuration) =>
        configuration.AllowedExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant());

    private static string Field(IReadOnlyDictionary<string, string> fields, string name) =>
        fields.TryGetValue(name, out var value) ? value : string.Empty;

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
            // Left for the sweep.
        }
    }
}
