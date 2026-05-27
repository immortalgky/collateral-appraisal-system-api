namespace Appraisal.Application.Features.SupportingDataMaintenance.SubmitSupportingData;

public record SubmitSupportingDataCommand(Guid? SupportingId, SupportingDataHeaderDto Header)
    : ICommand<SubmitSupportingDataResult>,
      ITransactionalCommand<IAppraisalUnitOfWork>;