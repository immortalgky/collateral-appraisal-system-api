using Microsoft.Extensions.Options;
using Shared.Configurations;

namespace Document.Domain.Documents.Features.UploadDocument;

public class UploadDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/documents",
                async (
                    HttpRequest request,
                    ISender sender,
                    IOptions<FileStorageConfiguration> fileStorageOptions,
                    CancellationToken cancellationToken) =>
                {
                    var maxFileSize = fileStorageOptions.Value.MaxFileSizeBytes;

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
                    var documentCategory = form["documentCategory"].ToString();
                    var description = form["description"].ToString();

                    if (string.IsNullOrWhiteSpace(documentType))
                        return Results.BadRequest("documentType is required");

                    if (string.IsNullOrWhiteSpace(documentCategory))
                        return Results.BadRequest("documentCategory is required");

                    var command = new UploadDocumentCommand(
                        form.Files[0],
                        uploadSessionId,
                        documentType,
                        documentCategory,
                        description
                    );

                    var result = await sender.Send(command, cancellationToken);

                    return Results.Ok(result);
                })
            .WithName("UploadDocument")
            .WithTags("Documents")
            .DisableAntiforgery()
            .Produces<UploadDocumentResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
            // .RequireAuthorization("CanUploadDocument");
    }
}