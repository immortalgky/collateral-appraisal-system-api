using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.SetSystemCalc;

public record SetSystemCalcCommand(
    Guid PricingAnalysisId,
    bool UseSystemCalc
) : ICommand<SetSystemCalcResult>, ITransactionalCommand<IAppraisalUnitOfWork>;