namespace Appraisal.Application.Features.SupportingDataMaintenance.DeleteSupportingDataById;

public record DeleteSupportingDataByIdCommand(Guid SupportingId)
    : ICommand,
      ITransactionalCommand<IAppraisalUnitOfWork>;