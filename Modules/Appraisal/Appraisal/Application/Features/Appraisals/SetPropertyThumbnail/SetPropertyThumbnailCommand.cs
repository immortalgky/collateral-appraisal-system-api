using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.SetPropertyThumbnail;

public record SetPropertyThumbnailCommand(
    Guid AppraisalPropertyId,
    Guid GalleryPhotoId
) : ICommand<SetPropertyThumbnailResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
