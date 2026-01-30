namespace Appraisal.Domain.MarketComparables;

/// <summary>
/// Repository interface for MarketComparable aggregate.
/// </summary>
public interface IMarketComparableRepository : IRepository<MarketComparable, Guid>
{
    Task<MarketComparable?> GetByComparableNumberAsync(string comparableNumber,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<MarketComparable>> SearchAsync(
        string? propertyType = null,
        string? province = null,
        string? district = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        bool activeOnly = true,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<MarketComparable>> GetNearbyAsync(
        decimal latitude,
        decimal longitude,
        decimal radiusKm,
        string? propertyType = null,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<MarketComparable>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<MarketComparable?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
}