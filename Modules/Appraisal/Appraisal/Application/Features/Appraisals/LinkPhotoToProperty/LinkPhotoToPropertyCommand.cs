using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.LinkPhotoToProperty;

public record LinkPhotoToPropertyCommand(
    Guid GalleryPhotoId,
    Guid AppraisalPropertyId,
    string PhotoPurpose,
    string? SectionReference,
    string LinkedBy
) : ICommand<LinkPhotoToPropertyResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
