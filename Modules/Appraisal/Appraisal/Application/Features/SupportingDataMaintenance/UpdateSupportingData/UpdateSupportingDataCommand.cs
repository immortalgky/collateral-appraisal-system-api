namespace Appraisal.Application.Features.SupportingDataMaintenance.UpdateSupportingData;

public record UpdateSupportingDataCommand(Guid SupportingId, SupportingDataHeaderDto Header)
    : ICommand<UpdateSupportingDataResult>,
      ITransactionalCommand<IAppraisalUnitOfWork>;