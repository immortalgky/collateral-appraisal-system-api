namespace Appraisal.Application.Features.MarketComparables.DeleteMarketComparable;

public record DeleteMarketComparableCommand(Guid Id) : ICommand<DeleteMarketComparableResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
