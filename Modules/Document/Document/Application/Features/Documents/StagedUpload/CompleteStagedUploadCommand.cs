using Document.Services;

namespace Document.Domain.Documents.Features.StagedUpload;

/// <summary>
/// Files bytes that are already on the share as a document, inside a transaction.
/// <para>
/// The streamed upload route needs this: it has to read the request body itself — the framework's
/// form reader would spool a gigabyte through the system drive first — which means the bytes are
/// written before there is a command to send. This carries the finished staging over into the
/// ordinary command pipeline, so the row is written and committed the way every other write is.
/// </para>
/// The path is produced by <see cref="IDocumentService.StageStreamAsync"/> in the same request and
/// never comes from a caller.
/// </summary>
public record CompleteStagedUploadCommand(
    Guid DocumentId,
    string StagedFilePath,
    StagedFileMetadata Metadata,
    string ChecksumBase64
) : ICommand<UploadDocument.UploadDocumentResult>, ITransactionalCommand<IDocumentUnitOfWork>;
