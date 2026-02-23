using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.AddAppendixDocument;

public record AddAppendixDocumentCommand(
    Guid AppendixId,
    Guid GalleryPhotoId,
    int DisplaySequence
) : ICommand<AddAppendixDocumentResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
