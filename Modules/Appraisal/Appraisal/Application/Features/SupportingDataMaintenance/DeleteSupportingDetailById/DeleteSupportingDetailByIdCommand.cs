namespace Appraisal.Application.Features.SupportingDataMaintenance.DeleteSupportingDetailById;

public record DeleteSupportingDetailByIdCommand(Guid SupportingId, Guid DetailId)
    : ICommand,
      ITransactionalCommand<IAppraisalUnitOfWork>;