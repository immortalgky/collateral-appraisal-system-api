using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableFactors.DeleteMarketComparableFactor;

/// <summary>
/// Command to soft delete a market comparable factor.
/// </summary>
public sealed record DeleteMarketComparableFactorCommand(
    Guid Id) : ICommand<DeleteMarketComparableFactorResult>;
