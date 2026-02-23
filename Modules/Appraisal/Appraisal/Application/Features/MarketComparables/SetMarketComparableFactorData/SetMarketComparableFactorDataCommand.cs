using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparables.SetMarketComparableFactorData;

/// <summary>
/// Command to set factor data for a market comparable
/// </summary>
public record SetMarketComparableFactorDataCommand(
    Guid MarketComparableId,
    List<FactorDataItem> FactorData
) : ICommand<SetMarketComparableFactorDataResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
