namespace Appraisal.Application.Features.SupportingDataMaintenance.UpdateSupportingDetail;

public record UpdateSupportingDetailCommand(Guid SupportingId, Guid Id, SupportingDataDetailDto Detail)
    : ICommand<UpdateSupportingDetailResult>,
      ITransactionalCommand<IAppraisalUnitOfWork>;
