namespace Appraisal.Domain.Quotations;

/// <summary>
/// Represents a document explicitly shared by the admin with invited external companies
/// for a specific quotation and appraisal.
///
/// Source (v7): `/requests/{requestId}/documents` — i.e., request.RequestDocuments
/// and request.RequestTitleDocuments. Each appraisal in the quotation has its own RequestId
/// and therefore its own distinct doc pool.
///
/// Level vocabulary:
///   "RequestLevel" — row in request.RequestDocuments (application-package document)
///   "TitleLevel"   — row in request.RequestTitleDocuments (per-title document)
///
/// Composite PK: (QuotationRequestId, DocumentId). `AppraisalId` is authoritative here —
/// each row corresponds to exactly one appraisal (appraisals do not share a RequestId).
/// </summary>
public class QuotationSharedDocument
{
    public const string RequestLevel = "RequestLevel";
    public const string TitleLevel = "TitleLevel";

    public Guid QuotationRequestId { get; private set; }
    public Guid AppraisalId { get; private set; }
    public Guid DocumentId { get; private set; }

    /// <summary>"RequestLevel" | "TitleLevel"</summary>
    public string Level { get; private set; } = null!;

    public DateTime SharedAt { get; private set; }

    /// <summary>UserId of the admin who shared this document.</summary>
    public string SharedBy { get; private set; } = null!;

    private QuotationSharedDocument()
    {
    }

    public static QuotationSharedDocument Create(
        Guid quotationRequestId,
        Guid appraisalId,
        Guid documentId,
        string level,
        string sharedBy)
    {
        if (level is not (RequestLevel or TitleLevel))
            throw new ArgumentException(
                $"Level must be '{RequestLevel}' or '{TitleLevel}'. Got: '{level}'", nameof(level));

        return new QuotationSharedDocument
        {
            QuotationRequestId = quotationRequestId,
            AppraisalId = appraisalId,
            DocumentId = documentId,
            Level = level,
            SharedAt = DateTime.UtcNow,
            SharedBy = sharedBy
        };
    }

    internal void Update(Guid appraisalId, string level, string sharedBy)
    {
        if (level is not (RequestLevel or TitleLevel))
            throw new ArgumentException(
                $"Level must be '{RequestLevel}' or '{TitleLevel}'. Got: '{level}'", nameof(level));

        AppraisalId = appraisalId;
        Level = level;
        SharedAt = DateTime.UtcNow;
        SharedBy = sharedBy;
    }
}
