using Shared.CQRS;

namespace Document.Domain.Documents.Features.DownloadDocument;

public record DownloadDocumentQuery(Guid Id, bool ForceDownload = false) : IQuery<DownloadDocumentResult>;
