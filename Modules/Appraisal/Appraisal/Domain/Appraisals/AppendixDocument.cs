namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Document attached to an appraisal appendix.
/// Links to exactly one source: an <see cref="AppraisalGallery"/> photo (images) or a
/// <c>document.Documents</c> row directly (PDFs, which never enter the gallery).
/// </summary>
public class AppendixDocument : Entity<Guid>
{
    public Guid AppraisalAppendixId { get; private set; }
    public Guid? GalleryPhotoId { get; private set; }
    public Guid? DocumentId { get; private set; }
    public int DisplaySequence { get; private set; }

    private AppendixDocument()
    {
    }

    public static AppendixDocument Create(
        Guid appraisalAppendixId,
        Guid? galleryPhotoId,
        Guid? documentId,
        int displaySequence)
    {
        RuleCheck.Valid()
            .AddErrorIf(galleryPhotoId is null && documentId is null,
                "Either GalleryPhotoId or DocumentId must be provided.")
            .AddErrorIf(galleryPhotoId is not null && documentId is not null,
                "Only one of GalleryPhotoId or DocumentId may be provided.")
            .ThrowIfInvalid();

        return new AppendixDocument
        {
            AppraisalAppendixId = appraisalAppendixId,
            GalleryPhotoId = galleryPhotoId,
            DocumentId = documentId,
            DisplaySequence = displaySequence
        };
    }

    public void SetDisplaySequence(int sequence)
    {
        DisplaySequence = sequence;
    }
}