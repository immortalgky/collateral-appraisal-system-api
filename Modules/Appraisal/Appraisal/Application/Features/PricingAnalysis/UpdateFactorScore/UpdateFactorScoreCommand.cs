using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.UpdateFactorScore;

/// <summary>
/// Command to update a factor score
/// </summary>
public record UpdateFactorScoreCommand(
    Guid PricingAnalysisId,
    Guid FactorScoreId,
    string? SubjectValue = null,
    decimal? SubjectScore = null,
    string? ComparableValue = null,
    decimal? ComparableScore = null,
    decimal? FactorWeight = null,
    decimal? AdjustmentPct = null,
    string? Remarks = null
) : ICommand<UpdateFactorScoreResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
