namespace Appraisal.Application.Features.Appraisals.AddAppendixDocument;

public record AddAppendixDocumentRequest(
    Guid? GalleryPhotoId,
    Guid? DocumentId,
    int DisplaySequence);
