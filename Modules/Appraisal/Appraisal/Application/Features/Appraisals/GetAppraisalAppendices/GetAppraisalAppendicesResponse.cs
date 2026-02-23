namespace Appraisal.Application.Features.Appraisals.GetAppraisalAppendices;

public record GetAppraisalAppendicesResponse(List<AppraisalAppendixResponse> Items);

public record AppraisalAppendixResponse(
    Guid Id,
    Guid AppendixTypeId,
    string AppendixTypeCode,
    string AppendixTypeName,
    int SortOrder,
    int LayoutColumns,
    List<AppendixDocumentResponse> Documents
);

public record AppendixDocumentResponse(
    Guid Id,
    Guid GalleryPhotoId,
    Guid DocumentId,
    int DisplaySequence
);
