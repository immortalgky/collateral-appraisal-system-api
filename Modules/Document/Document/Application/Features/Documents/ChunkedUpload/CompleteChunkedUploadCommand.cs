using Document.Domain.Documents.Features.UploadDocument;

namespace Document.Domain.Documents.Features.ChunkedUpload;

/// <summary>
/// Closes a chunked upload: the assembled file becomes a document. Answers with the same shape as
/// a single-request upload, so callers cannot tell which route a file took.
/// <para>
/// Deliberately not an <c>ITransactionalCommand</c>: the handler hashes and moves a file that can
/// be a gigabyte, and a transaction opened before it would hold a pooled connection across all of
/// that while issuing no SQL. The handler saves once at the end instead, which is atomic on its
/// own for the two rows involved.
/// </para>
/// </summary>
public record CompleteChunkedUploadCommand(Guid UploadId) : ICommand<UploadDocumentResult>;
