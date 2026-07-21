namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Valuation-document checklist attachment: an image or PDF attached against one of the
/// VAL_DOC document types (parameter.DocumentTypes, Category = 'VAL_DOC'). Each document
/// type may hold multiple attachments; each attachment links directly to a document.Documents
/// row by DocumentId (no gallery, no annotation). Mirrors the RequestDocument/CollateralDocument
/// pattern; standalone aggregate — not part of the Appraisal aggregate.
/// </summary>
public class AppraisalDocument : Aggregate<Guid>
{
    public Guid AppraisalId { get; private set; }
    public string DocumentTypeCode { get; private set; } = null!;
    public Guid DocumentId { get; private set; }
    public string FileName { get; private set; } = null!;
    public string? MimeType { get; private set; }
    public long? FileSizeBytes { get; private set; }
    public string? Notes { get; private set; }
    public int SortOrder { get; private set; }
    public string? UploadedByName { get; private set; }

    private AppraisalDocument()
    {
        // For EF Core
    }

    public static AppraisalDocument Create(
        Guid appraisalId,
        string documentTypeCode,
        Guid documentId,
        string fileName,
        string? mimeType,
        long? fileSizeBytes,
        string? notes,
        int sortOrder,
        string? uploadedByName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentTypeCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (documentId == Guid.Empty)
            throw new ArgumentException("DocumentId must not be empty.", nameof(documentId));

        return new AppraisalDocument
        {
            // NOTE: assign Guid v7 explicitly — do not rely on a NEWSEQUENTIALID default
            // (see AppendixDocument, which was flagged for that bug).
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId,
            DocumentTypeCode = documentTypeCode.Trim().ToUpperInvariant(),
            DocumentId = documentId,
            FileName = fileName,
            MimeType = mimeType,
            FileSizeBytes = fileSizeBytes,
            Notes = notes,
            SortOrder = sortOrder,
            UploadedByName = uploadedByName
        };
    }

    public void UpdateNotes(string? notes)
    {
        Notes = notes;
    }
}
