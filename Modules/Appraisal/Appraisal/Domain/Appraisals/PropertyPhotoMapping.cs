namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Links photos to specific appraisal properties.
/// </summary>
public class PropertyPhotoMapping : Entity<Guid>
{
    public Guid GalleryPhotoId { get; private set; }
    public Guid AppraisalPropertyId { get; private set; }

    // Mapping Details
    public string PhotoPurpose { get; private set; } = null!; // Evidence, Condition, Boundary
    public string? SectionReference { get; private set; } // Roof, Kitchen, Engine, etc.
    public int SequenceNumber { get; private set; } = 1;

    // Thumbnail
    public bool IsThumbnail { get; private set; }

    // Linked By
    public string LinkedBy { get; private set; } = null!;
    public DateTime LinkedAt { get; private set; }

    private PropertyPhotoMapping()
    {
    }

    public static PropertyPhotoMapping Create(
        Guid galleryPhotoId,
        Guid appraisalPropertyId,
        string photoPurpose,
        string linkedBy)
    {
        return new PropertyPhotoMapping
        {
            Id = Guid.CreateVersion7(),
            GalleryPhotoId = galleryPhotoId,
            AppraisalPropertyId = appraisalPropertyId,
            PhotoPurpose = photoPurpose,
            SequenceNumber = 1,
            LinkedBy = linkedBy,
            LinkedAt = DateTime.Now
        };
    }

    public void SetSection(string? sectionReference)
    {
        SectionReference = sectionReference;
    }

    public void SetSequence(int sequence)
    {
        SequenceNumber = sequence;
    }

    public void SetAsThumbnail()
    {
        IsThumbnail = true;
    }

    public void UnsetAsThumbnail()
    {
        IsThumbnail = false;
    }
}