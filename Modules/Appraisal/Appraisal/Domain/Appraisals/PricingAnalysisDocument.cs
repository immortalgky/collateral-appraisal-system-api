namespace Appraisal.Domain.Appraisals;


public class PricingAnalysisDocument : Entity<Guid>
{
    public Guid PricingAnalysisId { get; private set; }
    public Guid? DocumentId { get; private set; }
    public string? FileName { get; private set; }
    public string? FilePath { get; private set; }
    public string? UploadedBy { get; private set; }
    public string? UploadedByName { get; private set; }
    public DateTime? UploadedAt { get; private set; }

    private PricingAnalysisDocument()
    {
        // EF Core
    }

    private PricingAnalysisDocument(Guid pricingAnalysisId)
    {
        PricingAnalysisId = pricingAnalysisId;
    }

    internal static PricingAnalysisDocument Create(Guid pricingAnalysisId, PricingAnalysisDocumentData data)
    {
        return new PricingAnalysisDocument(pricingAnalysisId)
        {
            DocumentId = data.DocumentId,
            FileName = data.FileName,
            FilePath = data.FilePath,
            UploadedBy = data.UploadedBy,
            UploadedByName = data.UploadedByName,
            UploadedAt = data.UploadedAt
        };
    }

    internal (Guid? PreviousDocumentId, Guid? NewDocumentId) Update(PricingAnalysisDocumentData data)
    {
        Guid? previousDocumentId = null;
        Guid? newDocumentId = null;

        if (DocumentId != data.DocumentId)
        {
            previousDocumentId = DocumentId;
            newDocumentId = data.DocumentId;

            DocumentId = data.DocumentId;
            FileName = data.FileName;
            FilePath = data.FilePath;

            UploadedBy = data.DocumentId.HasValue ? data.UploadedBy : null;
            UploadedByName = data.DocumentId.HasValue ? data.UploadedByName : null;
            UploadedAt = data.DocumentId.HasValue ? data.UploadedAt : null;
        }

        return (previousDocumentId, newDocumentId);
    }
}

public record PricingAnalysisDocumentData(
    Guid? DocumentId,
    string? FileName,
    string? FilePath,
    string? UploadedBy,
    string? UploadedByName,
    DateTime? UploadedAt
);
