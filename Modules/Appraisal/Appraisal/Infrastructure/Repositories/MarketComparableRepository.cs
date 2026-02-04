namespace Appraisal.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for MarketComparable aggregate.
/// </summary>
public class MarketComparableRepository(AppraisalDbContext dbContext)
    : BaseRepository<MarketComparable, Guid>(dbContext), IMarketComparableRepository
{
    private readonly AppraisalDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public async Task<MarketComparable?> GetByComparableNumberAsync(string comparableNumber,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.MarketComparables
            .FirstOrDefaultAsync(m => m.ComparableNumber == comparableNumber, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<MarketComparable>> SearchAsync(
        string? propertyType = null,
        string? province = null,
        string? district = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.MarketComparables.AsQueryable();

        // if (activeOnly)
        //     query = query.Where(m => m.Status == "Active");

        if (!string.IsNullOrEmpty(propertyType))
            query = query.Where(m => m.PropertyType == propertyType);

        // if (!string.IsNullOrEmpty(province))
        //     query = query.Where(m => m.Province == province);

        // if (!string.IsNullOrEmpty(district))
        //     query = query.Where(m => m.District == district);

        // if (minPrice.HasValue)
        //     query = query.Where(m => m.TransactionPrice >= minPrice.Value);

        // if (maxPrice.HasValue)
        //     query = query.Where(m => m.TransactionPrice <= maxPrice.Value);

        return await query
            .OrderByDescending(m => m.InfoDateTime)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<IEnumerable<MarketComparable>> GetNearbyAsync(decimal latitude, decimal longitude, decimal radiusKm, string? propertyType = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
    
    // public async Task<IEnumerable<MarketComparable>> GetNearbyAsync(
    //     decimal latitude,
    //     decimal longitude,
    //     decimal radiusKm,
    //     string? propertyType = null,
    //     CancellationToken cancellationToken = default)
    // {
    //     // Simplified nearby search - in production, use spatial queries
    //     // For now, use basic bounding box calculation
    //     const decimal kmPerDegree = 111m; // approximate km per degree at equator
    //     var latDiff = radiusKm / kmPerDegree;
    //     var lonDiff = radiusKm / (kmPerDegree * (decimal)Math.Cos((double)latitude * Math.PI / 180));

    //     var query = _dbContext.MarketComparables
    //         .Where(m => m.Status == "Active")
    //         .Where(m => m.Latitude.HasValue && m.Longitude.HasValue)
    //         .Where(m => m.Latitude >= latitude - latDiff && m.Latitude <= latitude + latDiff)
    //         .Where(m => m.Longitude >= longitude - lonDiff && m.Longitude <= longitude + lonDiff);

    //     if (!string.IsNullOrEmpty(propertyType))
    //         query = query.Where(m => m.PropertyType == propertyType);

    //     return await query.ToListAsync(cancellationToken);
    // }

    /// <inheritdoc />
    public async Task<IEnumerable<MarketComparable>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.MarketComparables
            .OrderByDescending(m => m.CreatedOn)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MarketComparable?> GetByIdWithDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.MarketComparables
            .Include(m => m.FactorData)
                .ThenInclude(d => d.Factor)
            .Include(m => m.Images)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }
}