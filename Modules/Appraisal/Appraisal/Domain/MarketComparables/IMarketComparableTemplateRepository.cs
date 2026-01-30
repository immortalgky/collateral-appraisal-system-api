namespace Appraisal.Domain.MarketComparables;

/// <summary>
/// Repository interface for MarketComparableTemplate entity.
/// </summary>
public interface IMarketComparableTemplateRepository : IRepository<MarketComparableTemplate, Guid>
{
    Task<MarketComparableTemplate?> GetByCodeAsync(string templateCode, CancellationToken cancellationToken = default);
    Task<MarketComparableTemplate?> GetByIdWithFactorsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<MarketComparableTemplate>> GetByPropertyTypeAsync(string propertyType, bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<IEnumerable<MarketComparableTemplate>> GetAllAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
}
