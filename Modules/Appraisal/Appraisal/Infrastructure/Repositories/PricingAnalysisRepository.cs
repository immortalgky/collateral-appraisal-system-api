namespace Appraisal.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for PricingAnalysis aggregate.
/// </summary>
public class PricingAnalysisRepository(AppraisalDbContext dbContext)
    : BaseRepository<Domain.Appraisals.PricingAnalysis, Guid>(dbContext), IPricingAnalysisRepository
{
    private readonly AppraisalDbContext _dbContext = dbContext;

    // ── Subject lookups (PropertyGroup / ProjectModel) ────────────────────────

    /// <inheritdoc />
    public async Task<Domain.Appraisals.PricingAnalysis?> GetByPropertyGroupIdAsync(Guid propertyGroupId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PricingAnalyses
            .FirstOrDefaultAsync(
                pa => pa.SubjectType == PricingAnalysisSubjectType.PropertyGroup
                      && pa.AnchorId == propertyGroupId,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByPropertyGroupIdAsync(Guid propertyGroupId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PricingAnalyses
            .AnyAsync(
                pa => pa.SubjectType == PricingAnalysisSubjectType.PropertyGroup
                      && pa.AnchorId == propertyGroupId,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Domain.Appraisals.PricingAnalysis?> GetByProjectModelIdAsync(Guid projectModelId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PricingAnalyses
            .FirstOrDefaultAsync(
                pa => pa.SubjectType == PricingAnalysisSubjectType.ProjectModel
                      && pa.AnchorId == projectModelId,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByProjectModelIdAsync(Guid projectModelId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PricingAnalyses
            .AnyAsync(
                pa => pa.SubjectType == PricingAnalysisSubjectType.ProjectModel
                      && pa.AnchorId == projectModelId,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, ProjectModelPricingSummary>> GetProjectModelPricingSummariesAsync(
        IEnumerable<Guid> modelIds,
        CancellationToken cancellationToken = default)
    {
        var ids = modelIds.ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, ProjectModelPricingSummary>();

        var rows = await _dbContext.PricingAnalyses
            .Where(pa => pa.SubjectType == PricingAnalysisSubjectType.ProjectModel
                         && pa.AnchorId != null
                         && ids.Contains(pa.AnchorId!.Value))
            .Select(pa => new
            {
                ModelId = pa.AnchorId!.Value,
                pa.Id,
                pa.Status,
                pa.FinalAppraisedValue
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            r => r.ModelId,
            r => new ProjectModelPricingSummary(r.Id, r.Status, r.FinalAppraisedValue));
    }

    // ── Full-graph read ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Domain.Appraisals.PricingAnalysis?> GetByIdWithAllDataAsync(Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PricingAnalyses
            .Include(pa => pa.Documents)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.Calculations)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.ComparableLinks)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.ComparativeFactors)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.FactorScores)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.FinalValue)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.RsqResult)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.MachineCostItems)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.LeaseholdAnalysis!)
                        .ThenInclude(l => l.LandGrowthPeriods)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.LeaseholdAnalysis!)
                        .ThenInclude(l => l.TableRows)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.ProfitRentAnalysis!)
                        .ThenInclude(p => p.GrowthPeriods)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.ProfitRentAnalysis!)
                        .ThenInclude(p => p.TableRows)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.IncomeAnalysis!)
                        .ThenInclude(ia => ia.Sections)
                            .ThenInclude(s => s.Categories)
                                .ThenInclude(c => c.Assumptions)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.HypothesisAnalysis!)
                        .ThenInclude(ha => ha.Uploads)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.HypothesisAnalysis!)
                        .ThenInclude(ha => ha.LandBuildingUnitRows)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.HypothesisAnalysis!)
                        .ThenInclude(ha => ha.CondominiumUnitRows)
            .Include(pa => pa.Approaches)
                .ThenInclude(a => a.Methods)
                    .ThenInclude(m => m.HypothesisAnalysis!)
                        .ThenInclude(ha => ha.CostItems)
            .AsSplitQuery()
            .FirstOrDefaultAsync(pa => pa.Id == id, cancellationToken);
    }

    // ── Reference analysis methods ────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<Domain.Appraisals.PricingAnalysis>> GetReferencesByAnchorAsync(
        PricingAnalysisSubjectType subjectType,
        Guid anchorId,
        string? anchorRefKey = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.PricingAnalyses
            .Include(pa => pa.Approaches)
            .ThenInclude(a => a.Methods)
            .ThenInclude(m => m.FinalValue)
            .Where(pa => pa.SubjectType == subjectType && pa.AnchorId == anchorId);

        if (anchorRefKey is not null)
            query = query.Where(pa => pa.AnchorRefKey == anchorRefKey);

        return await query.ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetMethodIdsByAnalysisIdAsync(
        Guid pricingAnalysisId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PricingAnalyses
            .Where(pa => pa.Id == pricingAnalysisId)
            .SelectMany(pa => pa.Approaches)
            .SelectMany(a => a.Methods)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Domain.Appraisals.PricingAnalysis>> GetReferencesByHostMethodIdsAsync(
        IEnumerable<Guid> hostMethodIds,
        CancellationToken cancellationToken = default)
    {
        var ids = hostMethodIds.ToList();
        if (ids.Count == 0)
            return [];

        return await _dbContext.PricingAnalyses
            .Include(pa => pa.Approaches)
            .ThenInclude(a => a.Methods)
            .ThenInclude(m => m.FinalValue)
            .Where(pa => pa.HostMethodId != null && ids.Contains(pa.HostMethodId!.Value))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Domain.Appraisals.PricingAnalysis?> FindReferenceAsync(
        PricingAnalysisSubjectType subjectType,
        Guid anchorId,
        string? anchorRefKey = null,
        CancellationToken cancellationToken = default)
    {
        // Include Approaches so callers can read/guard the existing "Market" approach
        // (the idempotent-return paths in CreateOrGetReference / CreateReferenceFromMethod
        // rely on this — without it the in-memory collection is empty and a duplicate
        // approach would be inserted).
        return await _dbContext.PricingAnalyses
            .Include(pa => pa.Approaches)
            .FirstOrDefaultAsync(
                pa => pa.SubjectType == subjectType
                      && pa.AnchorId == anchorId
                      && pa.AnchorRefKey == anchorRefKey,
                cancellationToken);
    }

    // ── Bulk-delete helpers ──────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetMethodIdsForSubjectAsync(
        PricingAnalysisSubjectType subjectType,
        Guid anchorId,
        CancellationToken cancellationToken = default)
    {
        // Project to method ids directly — avoids loading the full PA graph.
        return await _dbContext.PricingAnalyses
            .Where(pa => pa.SubjectType == subjectType && pa.AnchorId == anchorId)
            .SelectMany(pa => pa.Approaches)
            .SelectMany(a => a.Methods)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteByHostMethodIdsAsync(
        IEnumerable<Guid> methodIds,
        CancellationToken cancellationToken = default)
    {
        var ids = methodIds.ToList();
        if (ids.Count == 0) return;

        var rows = await _dbContext.PricingAnalyses
            .Where(pa => pa.HostMethodId != null && ids.Contains(pa.HostMethodId!.Value))
            .ToListAsync(cancellationToken);

        if (rows.Count > 0)
            _dbContext.PricingAnalyses.RemoveRange(rows);
    }

    /// <inheritdoc />
    public async Task DeleteReferencesByAnchorAsync(
        PricingAnalysisSubjectType subjectType,
        Guid anchorId,
        string? anchorRefKey = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.PricingAnalyses
            .Where(pa => pa.SubjectType == subjectType && pa.AnchorId == anchorId);

        if (anchorRefKey is not null)
            query = query.Where(pa => pa.AnchorRefKey == anchorRefKey);

        var rows = await query.ToListAsync(cancellationToken);
        if (rows.Count > 0)
            _dbContext.PricingAnalyses.RemoveRange(rows);
    }

    /// <inheritdoc />
    public async Task DeleteRoomRefsByHostMethodExceptCodesAsync(
        Guid hostMethodId,
        IReadOnlyCollection<string> keepCodes,
        CancellationToken cancellationToken = default)
    {
        // Scope: only RoomIncomeRef rows owned by this income method.
        // Delete rows whose AnchorRefKey (room-type code) is no longer in the payload.
        var rows = await _dbContext.PricingAnalyses
            .Where(pa => pa.SubjectType == PricingAnalysisSubjectType.RoomIncomeRef
                         && pa.HostMethodId == hostMethodId
                         && (pa.AnchorRefKey == null || !keepCodes.Contains(pa.AnchorRefKey)))
            .ToListAsync(cancellationToken);

        if (rows.Count > 0)
            _dbContext.PricingAnalyses.RemoveRange(rows);
    }
}
