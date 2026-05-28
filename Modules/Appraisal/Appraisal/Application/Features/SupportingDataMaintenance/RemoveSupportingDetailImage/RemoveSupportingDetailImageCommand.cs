using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.SupportingDataMaintenance.RemoveSupportingDetailImage;

public record RemoveSupportingDetailImageCommand(
    Guid SupportingId,
    Guid DetailId,
    Guid ImageId
) : ICommand<RemoveSupportingDetailImageResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
