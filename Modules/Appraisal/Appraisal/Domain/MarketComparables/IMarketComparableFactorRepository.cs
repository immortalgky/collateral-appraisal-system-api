namespace Appraisal.Domain.MarketComparables;

/// <summary>
/// Repository interface for MarketComparableFactor entity.
/// </summary>
public interface IMarketComparableFactorRepository : IRepository<MarketComparableFactor, Guid>
{
    Task<MarketComparableFactor?> GetByCodeAsync(string factorCode, CancellationToken cancellationToken = default);
    Task<IEnumerable<MarketComparableFactor>> GetAllAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<IEnumerable<MarketComparableFactor>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
}
