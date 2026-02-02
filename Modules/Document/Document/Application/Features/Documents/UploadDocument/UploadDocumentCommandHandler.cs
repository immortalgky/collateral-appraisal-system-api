using Document.Data;
using Document.Services;
using Document.Domain.UploadSessions.Model;
using Shared.CQRS;
using Shared.Data;

namespace Document.Domain.Documents.Features.UploadDocument;

internal class UploadDocumentCommandHandler(
    IDocumentUnitOfWork uow,
    IDocumentRepository documentRepository,
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

        // Check for duplicate document
        await using var fileStream = command.File.OpenReadStream();
        var checksum = await documentService.CalculateChecksumAsync(fileStream, cancellationToken);

        logger.LogDebug("Calculated checksum {Checksum} for file {FileName}", checksum, command.File.FileName);

        // var duplicateExists = await documentRepository.ExistsAsync(d => d.Checksum == checksum, cancellationToken);
        // if (duplicateExists)
        // {
        //     logger.LogWarning(
        //         "Duplicate document detected with checksum {Checksum} for file {FileName}",
        //         checksum,
        //         command.File.FileName);
        //     throw new DomainException("A document with identical content already exists");
        // }

        // Upload document
        try
        {
            var documentId = await documentService.UploadAsync(
                command.File,
                command.UploadSessionId,
                command.DocumentType,
                command.DocumentCategory,
                command.Description,
                cancellationToken);

            logger.LogInformation(
                "Successfully uploaded document {DocumentId} for session {SessionId}. File: {FileName}",
                documentId,
                command.UploadSessionId,
                command.File.FileName);

            return new UploadDocumentResult(
                true,
                documentId,
                command.File.FileName,
                command.File.Length);
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