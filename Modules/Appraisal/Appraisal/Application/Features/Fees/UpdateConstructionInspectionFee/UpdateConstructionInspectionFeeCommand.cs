namespace Appraisal.Application.Features.Fees.UpdateConstructionInspectionFee;

public record UpdateConstructionInspectionFeeCommand(Guid FeeId, decimal? Amount)
    : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
