using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.UnsetPropertyThumbnail;

public record UnsetPropertyThumbnailCommand(
    Guid AppraisalPropertyId,
    Guid GalleryPhotoId
) : ICommand<UnsetPropertyThumbnailResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
