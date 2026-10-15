using Mapster;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared.Configurations;

namespace Document.Domain.Documents.Features.UploadDocument;

public class UploadDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // The server's own body cap is 30,000,000 bytes (~28.6 MB) by default — below the
        // MaxFileSizeBytes this endpoint advertises — and it rejects the request before any of the
        // checks below run. Kestrel and IIS in-process both read this off the endpoint, so one
        // attribute covers dev and production; the web.config maxAllowedContentLength the
        // deployment guide raises only covers IIS's own, separate check.
        var maxRequestBodyBytes = app.ServiceProvider
            .GetRequiredService<IOptions<FileStorageConfiguration>>().Value.MaxRequestBodyBytes;

        // Every refusal from this endpoint goes back as ProblemDetails, because that is the only
        // shape the SPA's error handling can read: a plain string body leaves `apiError.detail`
        // empty and the user is shown a generic message instead of the reason.
        static IResult Refuse(
            string detail,
            string title = "InvalidUpload",
            int statusCode = StatusCodes.Status400BadRequest) =>
            Results.Problem(detail: detail, title: title, statusCode: statusCode);

        app.MapPost("/documents",
                async (
                    HttpRequest request,
                    ISender sender,
                    IOptions<FileStorageConfiguration> fileStorageOptions,
                    CancellationToken cancellationToken) =>
                {
                    var maxFileSize = fileStorageOptions.Value.MaxFileSizeBytes;
                    var maxRequestBody = fileStorageOptions.Value.MaxRequestBodyBytes;

                    // Prevent memory exhaustion. The body, not the file: a multipart request is a
                    // little larger than what it carries. A body sent without a Content-Length skips
                    // this check and is stopped by the server itself, which answers 413 — that path
                    // is the exception, not the contract. Sizes in MiB, matching MaxFileSizeBytes and
                    // every limit quoted in the UI and the docs.
                    if (request.ContentLength > maxRequestBody)
                        return Refuse(
                            $"File too large. Maximum size is {maxFileSize / (1024 * 1024)}MB",
                            "PayloadTooLarge",
                            StatusCodes.Status413PayloadTooLarge);

                    var form = await request.ReadFormAsync(cancellationToken);

                    // Prevent IndexOutOfRangeException
                    if (form.Files.Count == 0)
                        return Refuse("No file uploaded");

                    // The pre-check above measures the whole request, which is allowed to be a
                    // megabyte larger than the file to carry the multipart envelope. Between the two
                    // sits a band where the file itself is over the limit: the validator catches it,
                    // but answers with its own raw text ("Validation failed: \n -- File.Length: …"),
                    // so say it here in the same words as every other size refusal.
                    if (form.Files[0].Length > maxFileSize)
                        return Refuse(
                            $"File too large. Maximum size is {maxFileSize / (1024 * 1024)}MB",
                            "PayloadTooLarge",
                            StatusCodes.Status413PayloadTooLarge);

                    // Prevent FormatException
                    if (!Guid.TryParse(form["uploadSessionId"], out var uploadSessionId))
                        return Refuse("Invalid or missing uploadSessionId");

                    var documentType = form["documentType"].ToString();
                    var documentCategory = form["documentCategory"].ToString();
                    var description = form["description"].ToString();

                    if (string.IsNullOrWhiteSpace(documentType))
                        return Refuse("documentType is required");

                    if (string.IsNullOrWhiteSpace(documentCategory))
                        return Refuse("documentCategory is required");

                    var command = new UploadDocumentCommand(
                        form.Files[0],
                        uploadSessionId,
                        documentType,
                        documentCategory,
                        description
                    );

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<UploadDocumentResponse>();
                    return Results.Ok(response);
                })
            .WithMetadata(new RequestSizeLimitAttribute(maxRequestBodyBytes))
            .WithName("UploadDocument")
            .WithTags("Documents")
            .DisableAntiforgery()
            .Produces<UploadDocumentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
            // .RequireAuthorization("CanUploadDocument");
    }
}