using Document.Services;
using Document.Domain.UploadSessions.Model;
using Shared.Data;

namespace Document.Domain.Documents.Features.UploadDocument;

internal class UploadDocumentCommandHandler(
    IDocumentUnitOfWork uow,
    IDocumentService documentService,
    ILogger<UploadDocumentCommandHandler> logger
) : ICommandHandler<UploadDocumentCommand, UploadDocumentResult>
{
    private readonly IRepository<UploadSession, Guid> _uploadSessionRepository =
        uow.Repository<UploadSession, Guid>();

    public async Task<UploadDocumentResult> Handle(UploadDocumentCommand command, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Starting document upload for session {SessionId}. File: {FileName}, Size: {FileSize} bytes",
            command.UploadSessionId,
            command.File.FileName,
            command.File.Length);

        // Validate upload session
        var session = await _uploadSessionRepository.GetByIdAsync(command.UploadSessionId, cancellationToken);
        if (session == null)
        {
            logger.LogWarning("Upload session {SessionId} not found", command.UploadSessionId);
            throw new NotFoundException($"Upload session {command.UploadSessionId} not found");
        }

        if (session.ExpiresAt < DateTime.Now)
        {
            logger.LogWarning(
                "Upload session {SessionId} has expired at {ExpiresAt}",
                command.UploadSessionId,
                session.ExpiresAt);
            throw new DomainException("Upload session has expired");
        }

        if (session.Status == "Completed")
        {
            logger.LogWarning("Upload session {SessionId} has already been completed", command.UploadSessionId);
            throw new DomainException("Upload session has already been completed");
        }

        // Duplicate uploads are currently allowed (dedup logic removed). The checksum that used to
        // be computed here fed that check and nothing else; it read the whole file a second time to
        // produce a value that was thrown away. The checksum stored on the document is computed by
        // DocumentService while the file is being copied.

        // Upload document
        try
        {
            var result = await documentService.UploadAsync(
                command.File,
                command.UploadSessionId,
                command.DocumentType,
                command.DocumentCategory,
                command.Description,
                cancellationToken);

            logger.LogInformation(
                "Successfully uploaded document {DocumentId} for session {SessionId}. File: {FileName}",
                result.DocumentId,
                command.UploadSessionId,
                command.File.FileName);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to upload document for session {SessionId}. File: {FileName}",
                command.UploadSessionId,
                command.File.FileName);
            throw;
        }
    }
}