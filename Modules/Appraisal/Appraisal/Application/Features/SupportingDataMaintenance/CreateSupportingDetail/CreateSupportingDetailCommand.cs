namespace Appraisal.Application.Features.SupportingDataMaintenance.CreateSupportingDetail;

public record CreateSupportingDetailCommand(Guid SupportingId, SupportingDataDetailDto Detail)
    : ICommand<CreateSupportingDetailResult>,
      ITransactionalCommand<IAppraisalUnitOfWork>;