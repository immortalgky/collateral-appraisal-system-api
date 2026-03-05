namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Images attached to law and regulation records.
/// </summary>
public class LawAndRegulationImage : Entity<Guid>
{
    public Guid LawAndRegulationId { get; private set; }
    public Guid GalleryPhotoId { get; private set; }

    // Image Details
    public int DisplaySequence { get; private set; }
    public string? Title { get; private set; }
    public string? Description { get; private set; }

    private LawAndRegulationImage()
    {
    }

    public static LawAndRegulationImage Create(
        Guid lawAndRegulationId,
        Guid galleryPhotoId,
        int displaySequence,
        string? title = null,
        string? description = null)
    {
        return new LawAndRegulationImage
        {
            LawAndRegulationId = lawAndRegulationId,
            GalleryPhotoId = galleryPhotoId,
            DisplaySequence = displaySequence,
            Title = title,
            Description = description
        };
    }

    public void SetDisplaySequence(int sequence)
    {
        DisplaySequence = sequence;
    }

    public void SetMetadata(string? title, string? description)
    {
        Title = title;
        Description = description;
    }
}