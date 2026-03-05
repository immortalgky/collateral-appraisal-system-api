namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Appraisal photo gallery entity.
/// Photos directly linked to Appraisal.
/// </summary>
public class AppraisalGallery : Entity<Guid>
{
    public Guid AppraisalId { get; private set; }
    public Guid DocumentId { get; private set; } // Reference to Document module

    // Photo Info
    public int PhotoNumber { get; private set; }
    public string PhotoType { get; private set; } = null!; // Exterior, Interior, Land, Defect, Document
    public string? PhotoCategory { get; private set; } // Front, Back, Kitchen, etc.
    public string? Caption { get; private set; }

    // GPS (from photo metadata or manual)
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }

    // Timestamps
    public DateTime? CapturedAt { get; private set; }
    public DateTime UploadedAt { get; private set; }

    // Usage tracking
    public bool IsInUse { get; private set; }

    // File Metadata (denormalized from Document module)
    public string? FileName { get; private set; }
    public string? FilePath { get; private set; }
    public string? FileExtension { get; private set; }
    public string? MimeType { get; private set; }
    public long? FileSizeBytes { get; private set; }

    // Audit
    public string UploadedBy { get; private set; } = null!;
    public string? UploadedByName { get; private set; }

    private AppraisalGallery()
    {
        // For EF Core
    }

    public static AppraisalGallery Create(
        Guid appraisalId,
        Guid documentId,
        int photoNumber,
        string photoType,
        string uploadedBy,
        string? fileName = null,
        string? filePath = null,
        string? fileExtension = null,
        string? mimeType = null,
        long? fileSizeBytes = null,
        string? uploadedByName = null)
    {
        return new AppraisalGallery
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId,
            DocumentId = documentId,
            PhotoNumber = photoNumber,
            PhotoType = photoType,
            IsInUse = false,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = uploadedBy,
            FileName = fileName,
            FilePath = filePath,
            FileExtension = fileExtension,
            MimeType = mimeType,
            FileSizeBytes = fileSizeBytes,
            UploadedByName = uploadedByName
        };
    }

    public void SetDetails(string? category, string? caption)
    {
        PhotoCategory = category;
        Caption = caption;
    }

    public void SetGps(decimal latitude, decimal longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public void SetCapturedAt(DateTime capturedAt)
    {
        CapturedAt = capturedAt;
    }

    public void MarkAsInUse()
    {
        IsInUse = true;
    }

    public void MarkAsNotInUse()
    {
        IsInUse = false;
    }
}