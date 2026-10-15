namespace Appraisal.Domain.Quotations;

/// <summary>
/// An entity owned by the <see cref="QuotationRequest"/> aggregate that links a Document
/// (from the Document module) to a quotation. Tracks whether the document was generated
/// server-side (Generated) or uploaded by the user (Uploaded).
/// Inherits <c>CreatedAt</c> and <c>CreatedBy</c> from <see cref="Entity{T}"/>.
/// </summary>
public class QuotationDocument : Entity<Guid>
{
    public Guid QuotationRequestId { get; private set; }
    public Guid DocumentId { get; private set; }

    /// <summary>"Summary" | "Upload"</summary>
    public string DocumentType { get; private set; } = default!;

    public string FileName { get; private set; } = default!;

    /// <summary>"Generated" | "Uploaded"</summary>
    public string Source { get; private set; } = default!;

    private QuotationDocument() { }

    internal static QuotationDocument Create(Guid quotationRequestId, QuotationDocumentData data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(data.DocumentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.FileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.Source);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.CreatedBy);

        return new QuotationDocument
        {
            Id = Guid.CreateVersion7(),
            QuotationRequestId = quotationRequestId,
            DocumentId = data.DocumentId,
            DocumentType = data.DocumentType,
            FileName = data.FileName,
            Source = data.Source,
            // Base Entity<> fields — set explicitly (AuditableEntityInterceptor also sets them on SaveChanges,
            // but we set them here for domain accuracy when returning the dto before SaveChanges runs).
            CreatedBy = data.CreatedBy,
            CreatedAt = data.CreatedAt
        };
    }
}

/// <summary>Data transfer object used by <see cref="QuotationRequest.AddDocument"/>.</summary>
public record QuotationDocumentData(
    Guid DocumentId,
    string DocumentType,
    string FileName,
    string Source,
    string CreatedBy,
    DateTime CreatedAt);
