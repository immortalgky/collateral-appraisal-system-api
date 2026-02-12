namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Links photos to specific property sections using polymorphic reference.
/// </summary>
public class PropertyPhotoMapping : Entity<Guid>
{
    public Guid GalleryPhotoId { get; private set; }

    // Polymorphic Property Reference
    public string PropertyDetailType { get; private set; } = null!; // Land, Building, Condo, Vehicle, Vessel, Machinery
    public Guid PropertyDetailId { get; private set; }

    // Mapping Details
    public string PhotoPurpose { get; private set; } = null!; // Evidence, Condition, Boundary
    public string? SectionReference { get; private set; } // Roof, Kitchen, Engine, etc.
    public int SequenceNumber { get; private set; } = 1;

    // Linked By
    public Guid LinkedBy { get; private set; }
    public DateTime LinkedAt { get; private set; }

    private PropertyPhotoMapping()
    {
    }

    public static PropertyPhotoMapping Create(
        Guid galleryPhotoId,
        string propertyDetailType,
        Guid propertyDetailId,
        string photoPurpose,
        Guid linkedBy)
    {
        return new PropertyPhotoMapping
        {
            Id = Guid.CreateVersion7(),
            GalleryPhotoId = galleryPhotoId,
            PropertyDetailType = propertyDetailType,
            PropertyDetailId = propertyDetailId,
            PhotoPurpose = photoPurpose,
            SequenceNumber = 1,
            LinkedBy = linkedBy,
            LinkedAt = DateTime.UtcNow
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
}