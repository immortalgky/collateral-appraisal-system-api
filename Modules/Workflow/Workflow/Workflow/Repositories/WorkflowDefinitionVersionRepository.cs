using Microsoft.EntityFrameworkCore;
using Workflow.Workflow.Models;

namespace Workflow.Workflow.Repositories;

public class WorkflowDefinitionVersionRepository : IWorkflowDefinitionVersionRepository
{
    private readonly WorkflowDbContext _context;
    private readonly DbSet<WorkflowDefinitionVersion> _dbSet;

    public WorkflowDefinitionVersionRepository(WorkflowDbContext context)
    {
        _context = context;
        _dbSet = context.WorkflowDefinitionVersions;
    }

    public async Task<WorkflowDefinitionVersion> AddAsync(
        WorkflowDefinitionVersion version, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(version, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return version;
    }

    public async Task<WorkflowDefinitionVersion> UpdateAsync(
        WorkflowDefinitionVersion version, CancellationToken cancellationToken = default)
    {
        _dbSet.Update(version);
        await _context.SaveChangesAsync(cancellationToken);
        return version;
    }

    public async Task<WorkflowDefinitionVersion?> GetVersionAsync(
        Guid definitionId, int version, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(v => v.DefinitionId == definitionId && v.Version == version, cancellationToken);
    }

    public async Task<WorkflowDefinitionVersion?> GetLatestVersionAsync(
        Guid definitionId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(v => v.DefinitionId == definitionId)
            .OrderByDescending(v => v.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<WorkflowDefinitionVersion?> GetLatestPublishedVersionAsync(
        Guid definitionId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(v => v.DefinitionId == definitionId && v.Status == VersionStatus.Published)
            .OrderByDescending(v => v.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<WorkflowDefinitionVersion>> GetAllVersionsAsync(
        Guid definitionId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(v => v.DefinitionId == definitionId)
            .OrderByDescending(v => v.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkflowDefinitionVersion>> GetVersionsByStatusAsync(
        VersionStatus status, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(v => v.Status == status)
            .OrderBy(v => v.DefinitionId)
            .ThenByDescending(v => v.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkflowDefinitionVersion>> GetAllPublishedVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(v => v.Status == VersionStatus.Published)
            .OrderBy(v => v.DefinitionId)
            .ThenByDescending(v => v.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        Guid definitionId, int version, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AnyAsync(v => v.DefinitionId == definitionId && v.Version == version, cancellationToken);
    }

    public async Task<WorkflowDefinitionVersion?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync([id], cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var version = await _dbSet.FindAsync([id], cancellationToken);
        if (version is null)
            return false;

        if (version.Status == VersionStatus.Published)
            return false;

        _dbSet.Remove(version);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<WorkflowDefinitionVersion>> GetVersionsEligibleForCleanupAsync(
        DateTime olderThan, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(v => v.Status == VersionStatus.Deprecated || v.Status == VersionStatus.Archived)
            .Where(v => v.CreatedAt < olderThan)
            .OrderBy(v => v.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkflowVersionStatistics> GetVersionStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        var versions = await _dbSet
            .GroupBy(v => v.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var categoryCounts = await _dbSet
            .GroupBy(v => v.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Category, g => g.Count, cancellationToken);

        var totalDefinitions = await _dbSet
            .Select(v => v.DefinitionId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new WorkflowVersionStatistics
        {
            TotalVersions = versions.Sum(v => v.Count),
            DraftVersions = versions.FirstOrDefault(v => v.Status == VersionStatus.Draft)?.Count ?? 0,
            PublishedVersions = versions.FirstOrDefault(v => v.Status == VersionStatus.Published)?.Count ?? 0,
            DeprecatedVersions = versions.FirstOrDefault(v => v.Status == VersionStatus.Deprecated)?.Count ?? 0,
            ArchivedVersions = versions.FirstOrDefault(v => v.Status == VersionStatus.Archived)?.Count ?? 0,
            TotalDefinitions = totalDefinitions,
            CategoryCounts = categoryCounts
        };
    }
}
