using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.UpdateGalleryPhoto;

public record UpdateGalleryPhotoCommand(
    Guid PhotoId,
    string? PhotoCategory,
    string? Caption,
    decimal? Latitude,
    decimal? Longitude,
    DateTime? CapturedAt
) : ICommand<UpdateGalleryPhotoResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
