using Document.Configurations;
using Document.Services;
using Mapster;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared.Identity;

namespace Document.Domain.Documents.Features.ChunkedUpload;

/// <summary>
/// Uploading a file in pieces, for files too big to send in one request.
///
/// Three calls: open the upload, send chunks at the offset the server is expecting, close it. The
/// server never holds more than one chunk in memory and never spools the whole file through the
/// system disk, and because each request is small it passes IIS and the load balancer on their
/// default settings — which is what makes a gigabyte possible without asking anyone to change
/// infrastructure.
/// </summary>
public class ChunkedUploadEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var maxChunkSizeBytes = app.ServiceProvider
            .GetRequiredService<IOptions<ChunkedUploadOptions>>().Value.MaxChunkSizeBytes;

        app.MapPost("/documents/chunked-uploads",
                async (InitChunkedUploadRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = request.Adapt<InitChunkedUploadCommand>();
                    var result = await sender.Send(command, cancellationToken);

                    return Results.Ok(new InitChunkedUploadResponse(result.UploadId));
                })
            .WithName("InitChunkedUpload")
            .WithTags("Documents")
            .Produces<InitChunkedUploadResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Raw bytes rather than a form: a multipart body would be parsed before the handler runs,
        // which is the buffering this route exists to avoid. No MediatR either — the pipeline
        // would have to hold the stream open across behaviours for nothing.
        app.MapPut("/documents/chunked-uploads/{uploadId:guid}",
                async (
                    Guid uploadId,
                    [FromQuery] long offset,
                    HttpRequest request,
                    IChunkedUploadStore store,
                    ICurrentUserService currentUserService,
                    CancellationToken cancellationToken) =>
                {
                    if (offset < 0)
                        return Results.Problem(
                            detail: "Offset cannot be negative.",
                            title: "InvalidOffset",
                            statusCode: StatusCodes.Status400BadRequest);

                    var meta = await store.TryReadMetaAsync(uploadId, cancellationToken);
                    if (meta is null)
                        return Results.Problem(
                            detail: $"Chunked upload {uploadId} not found. It may have expired.",
                            title: "NotFound",
                            statusCode: StatusCodes.Status404NotFound);

                    if (!OwnedByCurrentUser(meta, currentUserService))
                        return Results.Problem(
                            detail: "This upload belongs to another user.",
                            title: "Forbidden",
                            statusCode: StatusCodes.Status403Forbidden);

                    var result = await store.AppendAsync(
                        uploadId, offset, request.Body, meta.FileSizeBytes, cancellationToken);

                    return result.Status switch
                    {
                        ChunkAppendStatus.Ok =>
                            Results.Ok(new ChunkAcceptedResponse(result.ReceivedBytes)),

                        // The client sends this number straight back as its next offset, which is
                        // how a transfer picks up after a dropped connection without asking.
                        ChunkAppendStatus.OffsetMismatch =>
                            Results.Json(
                                new ChunkAcceptedResponse(result.ReceivedBytes),
                                statusCode: StatusCodes.Status409Conflict),

                        // 423, not 409: a client reads 409 as "here is the offset to resume from"
                        // and would take the ProblemDetails body for a resume answer with
                        // receivedBytes 0 — and start the whole transfer again from the beginning.
                        ChunkAppendStatus.Busy =>
                            Results.Problem(
                                detail: "Another chunk of this upload is being written. Retry shortly.",
                                title: "UploadBusy",
                                statusCode: StatusCodes.Status423Locked),

                        ChunkAppendStatus.TooLarge =>
                            Results.Problem(
                                detail: $"This chunk would take the upload past the {meta.FileSizeBytes} bytes it declared.",
                                title: "TooLarge",
                                statusCode: StatusCodes.Status400BadRequest),

                        _ => Results.Problem(
                            detail: $"Chunked upload {uploadId} not found. It may have expired.",
                            title: "NotFound",
                            statusCode: StatusCodes.Status404NotFound)
                    };
                })
            .WithMetadata(new RequestSizeLimitAttribute(maxChunkSizeBytes))
            .WithName("AppendChunkedUpload")
            .WithTags("Documents")
            .DisableAntiforgery()
            .Produces<ChunkAcceptedResponse>(StatusCodes.Status200OK)
            .Produces<ChunkAcceptedResponse>(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status423Locked)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge);

        app.MapPost("/documents/chunked-uploads/{uploadId:guid}/complete",
                async (Guid uploadId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new CompleteChunkedUploadCommand(uploadId), cancellationToken);

                    return Results.Ok(result.Adapt<UploadDocument.UploadDocumentResponse>());
                })
            .WithName("CompleteChunkedUpload")
            .WithTags("Documents")
            .Produces<UploadDocument.UploadDocumentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static bool OwnedByCurrentUser(ChunkedUploadMeta meta, ICurrentUserService currentUserService)
    {
        var owner = currentUserService.UserId?.ToString() ?? currentUserService.Username ?? "anonymous";
        return string.Equals(owner, meta.Owner, StringComparison.Ordinal);
    }
}

public record InitChunkedUploadRequest(
    Guid UploadSessionId,
    string FileName,
    long FileSizeBytes,
    string ContentType,
    string DocumentType,
    string DocumentCategory,
    string? Description);

public record InitChunkedUploadResponse(Guid UploadId);

public record ChunkAcceptedResponse(long ReceivedBytes);
