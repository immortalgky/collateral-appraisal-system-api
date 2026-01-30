using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.AddFactorScore;

/// <summary>
/// Command to add a factor score to a pricing calculation
/// </summary>
public record AddFactorScoreCommand(
    Guid PricingAnalysisId,
    Guid PricingCalculationId,
    Guid FactorId,
    decimal FactorWeight,
    string? SubjectValue = null,
    decimal? SubjectScore = null,
    string? ComparableValue = null,
    decimal? ComparableScore = null,
    decimal? AdjustmentPct = null,
    string? Remarks = null
) : ICommand<AddFactorScoreResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
