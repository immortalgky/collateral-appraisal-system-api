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

    // Report Usage
    public bool IsUsedInReport { get; private set; }
    public string? ReportSection { get; private set; } // Cover, PropertySection, etc.

    // Audit
    public Guid UploadedBy { get; private set; }

    private AppraisalGallery()
    {
    }

    public static AppraisalGallery Create(
        Guid appraisalId,
        Guid documentId,
        int photoNumber,
        string photoType,
        Guid uploadedBy)
    {
        return new AppraisalGallery
        {
            Id = Guid.NewGuid(),
            AppraisalId = appraisalId,
            DocumentId = documentId,
            PhotoNumber = photoNumber,
            PhotoType = photoType,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = uploadedBy
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

    public void MarkForReport(string section)
    {
        IsUsedInReport = true;
        ReportSection = section;
    }

    public void UnmarkFromReport()
    {
        IsUsedInReport = false;
        ReportSection = null;
    }
}