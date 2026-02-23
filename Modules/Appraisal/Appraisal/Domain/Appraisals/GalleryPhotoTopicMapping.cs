namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Join entity linking a gallery photo to a photo topic (many-to-many).
/// </summary>
public class GalleryPhotoTopicMapping : Entity<Guid>
{
    public Guid GalleryPhotoId { get; private set; }
    public Guid PhotoTopicId { get; private set; }

    private GalleryPhotoTopicMapping()
    {
    }

    public static GalleryPhotoTopicMapping Create(Guid galleryPhotoId, Guid photoTopicId)
    {
        return new GalleryPhotoTopicMapping
        {
            Id = Guid.CreateVersion7(),
            GalleryPhotoId = galleryPhotoId,
            PhotoTopicId = photoTopicId
        };
    }
}
