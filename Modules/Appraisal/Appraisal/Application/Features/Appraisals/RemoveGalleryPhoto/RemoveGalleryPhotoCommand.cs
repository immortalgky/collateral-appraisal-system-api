using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.RemoveGalleryPhoto;

public record RemoveGalleryPhotoCommand(
    Guid PhotoId
) : ICommand<RemoveGalleryPhotoResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
