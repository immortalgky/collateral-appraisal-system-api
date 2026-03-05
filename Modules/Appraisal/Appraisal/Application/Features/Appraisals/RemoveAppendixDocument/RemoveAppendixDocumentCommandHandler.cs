using Appraisal.Domain.Appraisals;
using Shared.CQRS;
using Shared.Exceptions;

namespace Appraisal.Application.Features.Appraisals.RemoveAppendixDocument;

public class RemoveAppendixDocumentCommandHandler(
    IAppraisalAppendixRepository repository,
    IAppraisalGalleryRepository galleryRepository
) : ICommandHandler<RemoveAppendixDocumentCommand, RemoveAppendixDocumentResult>
{
    public async Task<RemoveAppendixDocumentResult> Handle(
        RemoveAppendixDocumentCommand command,
        CancellationToken cancellationToken)
    {
        var appendix = await repository.GetByIdWithDocumentsAsync(command.AppendixId, cancellationToken)
                       ?? throw new NotFoundException(nameof(AppraisalAppendix), command.AppendixId);

        // Get the GalleryPhotoId before removing the document
        var doc = appendix.Documents.FirstOrDefault(d => d.Id == command.DocumentId);
        var galleryPhotoId = doc?.GalleryPhotoId;

        appendix.RemoveDocument(command.DocumentId);
        await repository.UpdateAsync(appendix, cancellationToken);

        // Check if photo is still linked anywhere; if not, mark as not in use
        if (galleryPhotoId.HasValue)
        {
            var stillLinked = await galleryRepository.IsPhotoLinkedAnywhereAsync(galleryPhotoId.Value, cancellationToken);
            if (!stillLinked)
            {
                var photo = await galleryRepository.GetByIdAsync(galleryPhotoId.Value, cancellationToken);
                photo?.MarkAsNotInUse();
            }
        }

        return new RemoveAppendixDocumentResult(true);
    }
}
