namespace Appraisal.Application.Features.DecisionSummary.UpdateForceSaleRate;

public record UpdateForceSaleRateCommand(Guid AppraisalId, decimal? ForceSellingRateOverride)
    : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
