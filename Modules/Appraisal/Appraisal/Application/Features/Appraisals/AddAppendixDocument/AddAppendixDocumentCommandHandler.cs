using Appraisal.Domain.Appraisals;
using Shared.CQRS;
using Shared.Exceptions;

namespace Appraisal.Application.Features.Appraisals.AddAppendixDocument;

public class AddAppendixDocumentCommandHandler(
    IAppraisalAppendixRepository repository,
    IAppraisalGalleryRepository galleryRepository
) : ICommandHandler<AddAppendixDocumentCommand, AddAppendixDocumentResult>
{
    public async Task<AddAppendixDocumentResult> Handle(
        AddAppendixDocumentCommand command,
        CancellationToken cancellationToken)
    {
        var appendix = await repository.GetByIdWithDocumentsAsync(command.AppendixId, cancellationToken)
                       ?? throw new NotFoundException(nameof(AppraisalAppendix), command.AppendixId);

        var document = appendix.AddDocument(
            command.GalleryPhotoId,
            command.DisplaySequence);

        await repository.UpdateAsync(appendix, cancellationToken);

        // Mark gallery photo as in use
        var photo = await galleryRepository.GetByIdAsync(command.GalleryPhotoId, cancellationToken);
        photo?.MarkAsInUse();

        return new AddAppendixDocumentResult(document.Id, appendix.Id);
    }
}
