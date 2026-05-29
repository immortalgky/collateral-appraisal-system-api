namespace Appraisal.Application.Features.SupportingDataMaintenance.CreateDraftSupportingData;

public record CreateDraftSupportingDataCommand(SupportingDataHeaderDto Header)
    : ICommand<CreateDraftSupportingDataResult>,
      ITransactionalCommand<IAppraisalUnitOfWork>;