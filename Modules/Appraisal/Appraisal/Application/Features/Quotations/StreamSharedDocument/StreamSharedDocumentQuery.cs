namespace Appraisal.Application.Features.Quotations.StreamSharedDocument;

public record StreamSharedDocumentQuery(
    Guid QuotationRequestId,
    Guid DocumentId) : IQuery<StreamSharedDocumentResult>;

public record StreamSharedDocumentResult(
    string FilePath,
    string MimeType,
    string FileName);
