namespace Appraisal.Application.Features.Appraisals.AddAppendixDocument;

public record AddAppendixDocumentRequest(
    Guid GalleryPhotoId,
    int DisplaySequence);
