namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Document attached to an appraisal appendix.
/// </summary>
public class AppendixDocument : Entity<Guid>
{
    public Guid AppraisalAppendixId { get; private set; }
    public Guid GalleryPhotoId { get; private set; }
    public int DisplaySequence { get; private set; }

    private AppendixDocument()
    {
    }

    public static AppendixDocument Create(
        Guid appraisalAppendixId,
        Guid galleryPhotoId,
        int displaySequence)
    {
        return new AppendixDocument
        {
            AppraisalAppendixId = appraisalAppendixId,
            GalleryPhotoId = galleryPhotoId,
            DisplaySequence = displaySequence
        };
    }

    public void SetDisplaySequence(int sequence)
    {
        DisplaySequence = sequence;
    }
}