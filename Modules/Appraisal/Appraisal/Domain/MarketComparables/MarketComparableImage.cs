namespace Appraisal.Domain.MarketComparables;

/// <summary>
/// Photos attached to market comparables.
/// Child entity of MarketComparable aggregate.
/// References gallery photos via GalleryPhotoId (from AppraisalGallery).
/// </summary>
public class MarketComparableImage : Entity<Guid>
{
    public Guid MarketComparableId { get; private set; }
    public Guid GalleryPhotoId { get; private set; }
    public int DisplaySequence { get; private set; }
    public string? Title { get; private set; }
    public string? Description { get; private set; }

    private MarketComparableImage() { }

    internal static MarketComparableImage Create(
        Guid marketComparableId,
        int displaySequence,
        Guid galleryPhotoId,
        string? title = null,
        string? description = null)
    {
        if (galleryPhotoId == Guid.Empty)
            throw new ArgumentException("GalleryPhotoId cannot be empty", nameof(galleryPhotoId));

        return new MarketComparableImage
        {
            //Id = Guid.CreateVersion7(),
            MarketComparableId = marketComparableId,
            DisplaySequence = displaySequence,
            GalleryPhotoId = galleryPhotoId,
            Title = title,
            Description = description
        };
    }

    internal void UpdateDetails(string? title, string? description)
    {
        Title = title;
        Description = description;
    }

    internal void UpdateSequence(int newSequence)
    {
        DisplaySequence = newSequence;
    }
}
