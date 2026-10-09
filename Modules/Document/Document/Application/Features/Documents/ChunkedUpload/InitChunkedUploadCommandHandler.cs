using Document.Domain.UploadSessions.Model;
using Document.Services;
using Shared.Data;
using Shared.Identity;
using Shared.Time;

namespace Document.Domain.Documents.Features.ChunkedUpload;

internal class InitChunkedUploadCommandHandler(
    IDocumentUnitOfWork uow,
    IChunkedUploadStore store,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    ILogger<InitChunkedUploadCommandHandler> logger
) : ICommandHandler<InitChunkedUploadCommand, InitChunkedUploadResult>
{
    private readonly IRepository<UploadSession, Guid> _uploadSessionRepository =
        uow.Repository<UploadSession, Guid>();

    public async Task<InitChunkedUploadResult> Handle(
        InitChunkedUploadCommand command,
        CancellationToken cancellationToken)
    {
        // The same three questions the single-request upload asks — asked here instead, before the
        // client spends several minutes sending a file that was never going to be accepted.
        var session = await _uploadSessionRepository.GetByIdAsync(command.UploadSessionId, cancellationToken);
        if (session is null)
            throw new NotFoundException($"Upload session {command.UploadSessionId} not found");

        if (session.ExpiresAt < dateTimeProvider.ApplicationNow)
            throw new DomainException("Upload session has expired");

        if (session.Status == "Completed")
            throw new DomainException("Upload session has already been completed");

        var uploadId = Guid.CreateVersion7();

        var meta = new ChunkedUploadMeta(
            uploadId,
            command.UploadSessionId,
            command.FileName,
            command.FileSizeBytes,
            command.ContentType,
            command.DocumentType,
            command.DocumentCategory,
            command.Description,
            Owner(),
            dateTimeProvider.ApplicationNow);

        await store.CreateAsync(meta, cancellationToken);

        logger.LogInformation(
            "Opened chunked upload {UploadId} for {FileName} ({FileSize} bytes) in session {SessionId}",
            uploadId, command.FileName, command.FileSizeBytes, command.UploadSessionId);

        return new InitChunkedUploadResult(uploadId);
    }

    private string Owner() =>
        currentUserService.UserId?.ToString() ?? currentUserService.Username ?? "anonymous";
}
