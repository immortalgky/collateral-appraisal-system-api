using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.AddAppraisalDocument;

/// <summary>
/// Links an already-uploaded document (from the Document module) to a VAL_DOC document type
/// checklist entry for an appraisal.
/// </summary>
public record AddAppraisalDocumentCommand(
    Guid AppraisalId,
    string DocumentTypeCode,
    Guid DocumentId,
    string FileName,
    string? MimeType,
    long? FileSizeBytes,
    string? Notes,
    int? SortOrder,
    string? UploadedByName
) : ICommand<AddAppraisalDocumentResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
