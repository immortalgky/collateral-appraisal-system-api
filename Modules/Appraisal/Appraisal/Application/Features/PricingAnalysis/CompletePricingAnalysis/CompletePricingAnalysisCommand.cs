using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.CompletePricingAnalysis;

/// <summary>
/// Command to complete pricing analysis (change status from InProgress to Completed)
/// </summary>
public record CompletePricingAnalysisCommand(
    Guid Id,
    decimal MarketValue,
    decimal AppraisedValue,
    decimal? ForcedSaleValue
) : ICommand<CompletePricingAnalysisResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
