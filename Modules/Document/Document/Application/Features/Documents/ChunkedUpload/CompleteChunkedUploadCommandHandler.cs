using Document.Domain.Documents.Features.UploadDocument;
using Document.Domain.UploadSessions.Model;
using Document.Services;
using Shared.Data;
using Shared.Identity;
using Shared.Time;

namespace Document.Domain.Documents.Features.ChunkedUpload;

internal class CompleteChunkedUploadCommandHandler(
    IDocumentUnitOfWork uow,
    IChunkedUploadStore store,
    IDocumentService documentService,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    ILogger<CompleteChunkedUploadCommandHandler> logger
) : ICommandHandler<CompleteChunkedUploadCommand, UploadDocumentResult>
{
    private readonly IRepository<Models.Document, Guid> _documentRepository =
        uow.Repository<Models.Document, Guid>();

    private readonly IRepository<UploadSession, Guid> _uploadSessionRepository =
        uow.Repository<UploadSession, Guid>();

    public async Task<UploadDocumentResult> Handle(
        CompleteChunkedUploadCommand command,
        CancellationToken cancellationToken)
    {
        // The document carries the upload's own id. That is what makes completing twice safe once
        // the first attempt has committed: the second finds the document that already exists.
        var existing = await _documentRepository.GetByIdAsync(command.UploadId, cancellationToken);
        if (existing is not null)
        {
            logger.LogInformation(
                "Chunked upload {UploadId} was already completed — returning the existing document",
                command.UploadId);
            return ToResult(existing);
        }

        // Before it has committed, the id is not enough: a client that gave up waiting would start
        // the same work again. Holding the upload for the duration is what closes that window.
        using var claim = await store.TryClaimAsync(command.UploadId, cancellationToken);
        if (claim is null)
            throw new NotFoundException($"Chunked upload {command.UploadId} not found");

        var meta = claim.Meta;

        if (!OwnedByCurrentUser(meta))
            throw new UnauthorizedAccessException("This upload belongs to another user.");

        var received = await store.GetReceivedBytesAsync(command.UploadId, cancellationToken);
        if (received is null)
            throw new NotFoundException($"Chunked upload {command.UploadId} not found");

        if (received != meta.FileSizeBytes)
            throw new ConflictException(
                $"The upload is not complete: {received} of {meta.FileSizeBytes} bytes received.");

        // A resumable upload is meant to span a long time, and the session it belongs to expires
        // after a day. Checked here so that lands as an answer the app can show, rather than as
        // the session aggregate throwing on the way past.
        await EnsureSessionStillAcceptsDocuments(meta.UploadSessionId, cancellationToken);

        var result = await documentService.CreateFromStagedFileAsync(
            command.UploadId,
            store.DataPath(command.UploadId),
            meta,
            cancellationToken);

        // Saved here rather than by the transactional behaviour: hashing and moving a gigabyte
        // takes long enough that holding a database transaction across it would pin a pooled
        // connection doing no work. One SaveChanges is atomic on its own — the session counter and
        // the document row go together or not at all.
        await uow.SaveChangesAsync(cancellationToken);

        // Only after the row is committed. Dropped earlier, a failed commit would leave the bytes
        // in the documents folder with nothing pointing at them and no way to resume.
        claim.Dispose();
        store.Delete(command.UploadId);

        logger.LogInformation(
            "Completed chunked upload {UploadId} as document {DocumentId} ({FileSize} bytes)",
            command.UploadId, result.DocumentId, result.FileSize);

        return result;
    }

    private async Task EnsureSessionStillAcceptsDocuments(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await _uploadSessionRepository.GetByIdAsync(sessionId, cancellationToken);
        if (session is null)
            throw new NotFoundException($"Upload session {sessionId} not found");

        if (session.ExpiresAt < dateTimeProvider.ApplicationNow)
            throw new ConflictException(
                "The upload session expired while the file was being sent. Start the upload again.");

        if (session.Status == "Completed")
            throw new ConflictException("The upload session has already been completed.");
    }

    private bool OwnedByCurrentUser(ChunkedUploadMeta meta)
    {
        var owner = currentUserService.UserId?.ToString() ?? currentUserService.Username ?? "anonymous";
        return string.Equals(owner, meta.Owner, StringComparison.Ordinal);
    }

    private static UploadDocumentResult ToResult(Models.Document document) => new(
        document.Id,
        document.FileName,
        document.FileExtension,
        document.FileSizeBytes,
        document.MimeType,
        document.StorageUrl,
        document.DocumentType,
        document.DocumentCategory,
        document.Description,
        document.UploadedBy,
        document.UploadedByName,
        document.UploadedAt);
}
