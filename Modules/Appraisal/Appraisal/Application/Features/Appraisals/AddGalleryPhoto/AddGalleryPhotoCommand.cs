using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.AddGalleryPhoto;

public record AddGalleryPhotoCommand(
    Guid AppraisalId,
    Guid DocumentId,
    string PhotoType,
    string UploadedBy,
    string? PhotoCategory = null,
    string? Caption = null,
    decimal? Latitude = null,
    decimal? Longitude = null,
    DateTime? CapturedAt = null,
    Guid? PhotoTopicId = null
) : ICommand<AddGalleryPhotoResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
