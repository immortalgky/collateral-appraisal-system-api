namespace Appraisal.Application.Features.SupportingDataMaintenance.CreateSupportingData;

public record CreateSupportingDataCommand(SupportingDataHeaderDto Header)
    : ICommand<CreateSupportingDataResult>,
      ITransactionalCommand<IAppraisalUnitOfWork>;