using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableFactors.SetMarketComparableFactorStatus;

public record SetMarketComparableFactorStatusCommand(
    Guid Id,
    bool IsActive
) : ICommand<SetMarketComparableFactorStatusResult>;
