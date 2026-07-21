using FluentValidation;

namespace Appraisal.Application.Features.Appraisals.AddAppendixDocument;

public class AddAppendixDocumentCommandValidator : AbstractValidator<AddAppendixDocumentCommand>
{
    public AddAppendixDocumentCommandValidator()
    {
        // Exactly one of GalleryPhotoId (images, gallery path) or DocumentId (PDFs, direct
        // document path) must be supplied — mirrors the domain XOR invariant, caught here
        // for a clean 400 at the boundary instead of a DomainException.
        RuleFor(x => x)
            .Must(x => x.GalleryPhotoId.HasValue ^ x.DocumentId.HasValue)
            .WithMessage("Exactly one of galleryPhotoId or documentId must be provided.");
    }
}
