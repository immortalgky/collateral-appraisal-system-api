namespace Appraisal.Application.Features.SupportingDataMaintenance.UpdateDraftSupportingData;

public record UpdateDraftSupportingDataCommand(Guid SupportingId, SupportingDataHeaderDto Header)
    : ICommand<UpdateDraftSupportingDataResult>,
      ITransactionalCommand<IAppraisalUnitOfWork>;