using Document.Domain.Documents.Features.UploadDocument;
using Document.Services;

namespace Document.Domain.Documents.Features.StagedUpload;

internal class CompleteStagedUploadCommandHandler(IDocumentService documentService)
    : ICommandHandler<CompleteStagedUploadCommand, UploadDocumentResult>
{
    public Task<UploadDocumentResult> Handle(
        CompleteStagedUploadCommand command,
        CancellationToken cancellationToken) =>
        documentService.CreateFromStagedFileAsync(
            command.DocumentId,
            command.StagedFilePath,
            command.Metadata,
            command.ChecksumBase64,
            cancellationToken);
}
