using Dapper;

namespace Appraisal.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Appraisal aggregate
/// </summary>
public class AppraisalRepository(AppraisalDbContext dbContext, ISqlConnectionFactory connectionFactory)
    : BaseRepository<Domain.Appraisals.Appraisal, Guid>(dbContext), IAppraisalRepository
{
    private readonly AppraisalDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public async Task<Domain.Appraisals.Appraisal?> GetByIdWithPropertiesAsync(Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Appraisals
            .Include(a => a.Properties)
            .ThenInclude(p => p.LandDetail)
            .Include(a => a.Properties)
            .ThenInclude(p => p.BuildingDetail)
            .Include(p => p.Properties)
            .ThenInclude(p => p.CondoDetail)
            .Include(p => p.Properties)
            .ThenInclude(p => p.VehicleDetail)
            .Include(p => p.Properties)
            .ThenInclude(p => p.VesselDetail)
            .Include(p => p.Properties)
            .ThenInclude(p => p.MachineryDetail)
            .Include(p => p.Properties)
            .ThenInclude(p => p.LeaseAgreementDetail)
            .Include(p => p.Properties)
            .ThenInclude(p => p.RentalInfo)
            .ThenInclude(r => r.UpFrontEntries)
            .Include(p => p.Properties)
            .ThenInclude(p => p.RentalInfo)
            .ThenInclude(r => r.GrowthPeriodEntries)
            .Include(p => p.Properties)
            .ThenInclude(p => p.RentalInfo)
            .ThenInclude(r => r.ScheduleEntries)
            .Include(p => p.Properties)
            .ThenInclude(p => p.RentalInfo)
            .ThenInclude(r => r.ScheduleOverrides)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Domain.Appraisals.Appraisal?> GetByIdWithAllDataAsync(Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Appraisals
            .Include(a => a.Properties)
            .Include(a => a.Groups)
            .ThenInclude(g => g.Items)
            .Include(a => a.Assignments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Domain.Appraisals.Appraisal?> GetByAppraisalNumberAsync(string appraisalNumber,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Appraisals
            .FirstOrDefaultAsync(a => a.AppraisalNumber == appraisalNumber, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Domain.Appraisals.Appraisal?> GetByRequestIdAsync(Guid requestId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Appraisals
            .FirstOrDefaultAsync(a => a.RequestId == requestId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Appraisals
            .AnyAsync(a => a.RequestId == requestId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Domain.Appraisals.Appraisal>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Appraisals
            .Include(a => a.Properties)
            .Include(a => a.Groups)
            .Include(a => a.Assignments)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AppraisalSummary>> GetSummariesAsync(
        IReadOnlyList<Guid> appraisalIds,
        CancellationToken cancellationToken = default)
    {
        if (appraisalIds.Count == 0)
            return Array.Empty<AppraisalSummary>();

        var connection = connectionFactory.GetOpenConnection();

        // PropertyType: first CollateralType code from request.RequestTitles for this request.
        // PropertyLocation: Province + District from the first LandAppraisalDetail row (same
        //   derivation as vw_AppraisalList). EstimatedValue is not stored at appraisal level —
        //   always null here; callers (admin) set it later via EditDraftQuotation.
        var rows = await connection.QueryAsync<AppraisalSummaryRow>(
            """
            SELECT a.Id          AS AppraisalId,
                   a.AppraisalNumber,
                   a.RequestId,
                   (SELECT TOP 1 rp.PropertyType
                    FROM [request].[RequestProperties] rp
                    WHERE rp.RequestId = a.RequestId
                    ORDER BY rp.Id)                           AS PropertyType,
                   NULLIF(RTRIM(
                       ISNULL((SELECT TOP 1 Province
                               FROM [appraisal].[LandAppraisalDetails]
                               WHERE AppraisalPropertyId IN (
                                   SELECT Id FROM [appraisal].[AppraisalProperties]
                                   WHERE AppraisalId = a.Id)
                               ORDER BY (SELECT NULL)), '')
                       + CASE
                           WHEN (SELECT TOP 1 District
                                 FROM [appraisal].[LandAppraisalDetails]
                                 WHERE AppraisalPropertyId IN (
                                     SELECT Id FROM [appraisal].[AppraisalProperties]
                                     WHERE AppraisalId = a.Id)
                                 ORDER BY (SELECT NULL)) IS NOT NULL
                           THEN ' ' + (SELECT TOP 1 District
                                       FROM [appraisal].[LandAppraisalDetails]
                                       WHERE AppraisalPropertyId IN (
                                           SELECT Id FROM [appraisal].[AppraisalProperties]
                                           WHERE AppraisalId = a.Id)
                                       ORDER BY (SELECT NULL))
                           ELSE ''
                         END), '') AS PropertyLocation
            FROM [appraisal].[Appraisals] a
            WHERE a.Id IN @AppraisalIds
            """,
            new { AppraisalIds = appraisalIds.ToArray() });

        return rows
            .Select(r => new AppraisalSummary(
                AppraisalId: r.AppraisalId,
                AppraisalNumber: r.AppraisalNumber,
                PropertyType: r.PropertyType,
                PropertyLocation: string.IsNullOrWhiteSpace(r.PropertyLocation) ? null : r.PropertyLocation,
                EstimatedValue: null,
                RequestId: r.RequestId == Guid.Empty ? null : r.RequestId))
            .ToList()
            .AsReadOnly();
    }

    private sealed record AppraisalSummaryRow(
        Guid AppraisalId,
        string? AppraisalNumber,
        Guid RequestId,
        string? PropertyType,
        string? PropertyLocation);
}