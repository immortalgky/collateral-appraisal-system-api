using Workflow.Data;
using Workflow.Workflow.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Shared.Data;

namespace Workflow.Workflow.Repositories;

public class WorkflowInstanceRepository(
    WorkflowDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<WorkflowInstanceRepository> logger) : IWorkflowInstanceRepository
{
    public async Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkflowInstances
            .Include(x => x.WorkflowDefinition)
            .Include(x => x.ActivityExecutions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetByStatus(WorkflowStatus status,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkflowInstances
            .Where(x => x.Status == status)
            .Include(x => x.WorkflowDefinition)
            .OrderByDescending(x => x.StartedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetByAssignee(string assignee,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkflowInstances
            .Where(x => x.CurrentAssignee == assignee && x.Status == WorkflowStatus.Running)
            .Include(x => x.WorkflowDefinition)
            .OrderBy(x => x.StartedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetByWorkflowDefinition(Guid workflowDefinitionId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkflowInstances
            .Where(x => x.WorkflowDefinitionId == workflowDefinitionId)
            .Include(x => x.WorkflowDefinition)
            .OrderByDescending(x => x.StartedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkflowInstance?> GetByCorrelationId(string correlationId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkflowInstances
            .Include(x => x.WorkflowDefinition)
            .Include(x => x.ActivityExecutions)
            .FirstOrDefaultAsync(x => x.CorrelationId == correlationId, cancellationToken);
    }

    public async Task<WorkflowInstance?> GetByCorrelationIdAsync(
        string correlationId,
        string workflowDefinitionName,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkflowInstances
            .Include(x => x.WorkflowDefinition)
            .Include(x => x.ActivityExecutions)
            .FirstOrDefaultAsync(
                x => x.CorrelationId == correlationId
                     && x.WorkflowDefinition.Name == workflowDefinitionName,
                cancellationToken);
    }

    public async Task<WorkflowInstance?> GetWithExecutionsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkflowInstances
            .Include(x => x.WorkflowDefinition)
            .Include(x => x.ActivityExecutions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        await dbContext.WorkflowInstances.AddAsync(instance, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetRunningInstances(CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkflowInstances
            .AsSplitQuery()
            .Where(x => x.Status == WorkflowStatus.Running)
            .Include(x => x.WorkflowDefinition)
            .Include(x => x.ActivityExecutions)
            .OrderBy(x => x.StartedOn).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        dbContext.WorkflowInstances.Update(instance);

        await Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await LoggerAsync(cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // SaveChanges flushes EVERY tracked entity, so the losing command is not necessarily
            // the WorkflowInstance — WorkflowBookmark carries a rowversion too. Name the entity and
            // both token values so the racing writer can be identified from the log alone.
            await LogConcurrencyConflictAsync(ex, cancellationToken);
            throw;
        }
    }

    private async Task LogConcurrencyConflictAsync(
        DbUpdateConcurrencyException ex,
        CancellationToken cancellationToken)
    {
        foreach (var entry in ex.Entries)
        {
            try
            {
                var key = entry.Metadata.FindPrimaryKey()?.Properties
                    .Select(p => entry.Property(p.Name).CurrentValue)
                    .FirstOrDefault();

                var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken);

                logger.LogError(
                    "CONCURRENCY: {Entity} key={Key} state={State} rowExistsInDb={RowExists} " +
                    "originalToken={OriginalToken} databaseToken={DatabaseToken} " +
                    "dbUpdatedBy={DbUpdatedBy} dbUpdatedOn={DbUpdatedOn} " +
                    "mineActivity={MineActivity}/{MineStatus} dbActivity={DbActivity}/{DbStatus}",
                    entry.Metadata.ClrType.Name,
                    key,
                    entry.State,
                    databaseValues is not null,
                    DescribeConcurrencyToken(entry, entry.OriginalValues),
                    databaseValues is null ? "<row-missing>" : DescribeConcurrencyToken(entry, databaseValues),
                    // Audit columns identify the winning writer; activity/status reveal whether the
                    // winner was a duplicate resume that already advanced the workflow past us.
                    Peek(databaseValues, "UpdatedBy"),
                    Peek(databaseValues, "UpdatedAt"),
                    Peek(entry.CurrentValues, "CurrentActivityId"),
                    Peek(entry.CurrentValues, "Status"),
                    Peek(databaseValues, "CurrentActivityId"),
                    Peek(databaseValues, "Status"));

                // The columns the winning writer actually changed — compare what WE loaded against
                // what is in the row now. This names the racer far more precisely than the audit
                // columns can (every consumer and background job stamps UpdatedBy='anonymous').
                if (databaseValues is not null)
                    logger.LogError("CONCURRENCY: {Entity} key={Key} columns changed by the winning writer: {Changes}",
                        entry.Metadata.ClrType.Name, key, DescribeDrift(entry.OriginalValues, databaseValues));
            }
            catch (Exception logEx)
            {
                // Diagnostics must never mask the original concurrency failure.
                logger.LogWarning(logEx, "CONCURRENCY: failed to describe conflicting entry {Entity}",
                    entry.Metadata.ClrType.Name);
            }
        }
    }

    /// <summary>
    /// Lists properties whose value in the database differs from the value we originally loaded,
    /// i.e. exactly what the writer that beat us touched. The concurrency token itself is skipped
    /// (it always differs, that is the whole point).
    /// </summary>
    private static string DescribeDrift(PropertyValues mine, PropertyValues theirs)
    {
        var changes = new List<string>();

        foreach (var property in theirs.Properties)
        {
            if (property.IsConcurrencyToken) continue;
            if (mine.Properties.All(p => p.Name != property.Name)) continue;

            var mineValue = Stringify(mine[property.Name]);
            var theirValue = Stringify(theirs[property.Name]);

            if (mineValue == theirValue) continue;

            // Dictionary-valued columns (notably Variables) share a long identical prefix, so a raw
            // truncated string diff hides the one key that actually moved. Diff by key instead.
            if (mine[property.Name] is IDictionary<string, object> mineMap
                && theirs[property.Name] is IDictionary<string, object> theirMap)
            {
                changes.Add($"{property.Name}: {DescribeMapDrift(mineMap, theirMap)}");
                continue;
            }

            changes.Add($"{property.Name}: '{Truncate(mineValue)}' -> '{Truncate(theirValue)}'");
        }

        return changes.Count == 0 ? "<none — token bumped with no column change>" : string.Join(" | ", changes);
    }

    /// <summary>Per-key diff of two variable maps: added, removed and changed keys only.</summary>
    private static string DescribeMapDrift(
        IDictionary<string, object> mine,
        IDictionary<string, object> theirs)
    {
        var parts = new List<string>();

        foreach (var (key, theirValue) in theirs)
        {
            if (!mine.TryGetValue(key, out var mineValue))
            {
                parts.Add($"+{key}='{Truncate(Stringify(theirValue))}'");
                continue;
            }

            var mineText = Stringify(mineValue);
            var theirText = Stringify(theirValue);
            if (mineText != theirText)
                parts.Add($"~{key}: '{Truncate(mineText)}' -> '{Truncate(theirText)}'");
        }

        foreach (var key in mine.Keys.Where(k => !theirs.ContainsKey(k)))
            parts.Add($"-{key}");

        return parts.Count == 0 ? "<no key-level difference>" : string.Join(", ", parts);
    }

    private static string Stringify(object? value)
    {
        if (value is null) return "<null>";
        if (value is string s) return s;
        if (value is byte[] bytes) return "0x" + Convert.ToHexString(bytes);
        if (value.GetType().IsPrimitive || value is DateTime or DateTimeOffset or Guid or decimal)
            return value.ToString() ?? "<null>";

        // Value-converted properties (Variables, RuntimeOverrides, ActiveBranchActivities) surface as
        // mutable CLR objects; reference comparison would report every one of them as changed.
        try
        {
            return JsonSerializer.Serialize(value);
        }
        catch
        {
            return value.ToString() ?? "<null>";
        }
    }

    private static string Truncate(string value) =>
        value.Length <= 300 ? value : value[..300] + "…";

    /// <summary>Reads a property by name if the entity actually has it; never throws.</summary>
    private static string Peek(PropertyValues? values, string propertyName)
    {
        if (values is null) return "<n/a>";
        if (values.Properties.All(p => p.Name != propertyName)) return "<n/a>";

        return values[propertyName]?.ToString() ?? "<null>";
    }

    private static string DescribeConcurrencyToken(EntityEntry entry, PropertyValues values)
    {
        var token = entry.Metadata.GetProperties().FirstOrDefault(p => p.IsConcurrencyToken);
        if (token is null) return "<none>";

        return values[token.Name] switch
        {
            byte[] bytes => "0x" + Convert.ToHexString(bytes),
            null => "<null>",
            var other => other.ToString() ?? "<null>"
        };
    }

    public async Task LoggerAsync(CancellationToken cancellationToken = default)
    {
        var changedEntries = dbContext.ChangeTracker.Entries()
            .Where(e => e.State != EntityState.Unchanged)
            .ToList();

        if (changedEntries.Any())
        {
            Console.WriteLine($"Saving {changedEntries.Count} changed entities");

            foreach (var entry in changedEntries)
            {
                var entityName = entry.Entity.GetType().Name;
                var keyValue = entry.Metadata.FindPrimaryKey()?.Properties
                    .Select(p => entry.Property(p.Name).CurrentValue)
                    .FirstOrDefault();

                Console.WriteLine($"Entity {entityName} (Key: {keyValue}) - State: {entry.State}");
            }
        }

        await Task.CompletedTask;
    }

    public async Task<WorkflowInstance?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Get instance for update with full includes for optimistic locking
        return await dbContext.WorkflowInstances
            .Include(x => x.WorkflowDefinition)
            .Include(x => x.ActivityExecutions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<bool> TryUpdateWithConcurrencyAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        try
        {
            dbContext.WorkflowInstances.Update(instance);
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task<List<WorkflowInstance>> ListRunningByDefinitionIdAsync(Guid definitionId, CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkflowInstances
            .Where(x => x.WorkflowDefinitionId == definitionId && x.Status == WorkflowStatus.Running)
            .OrderBy(x => x.StartedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkflowInstance>> ListRunningByVersionIdAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkflowInstances
            .Where(x => x.WorkflowDefinitionVersionId == versionId && x.Status == WorkflowStatus.Running)
            .OrderBy(x => x.StartedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetLongRunningWorkflowsAsync(
        TimeSpan timeout,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        var cutoffTime = dateTimeProvider.ApplicationNow.Subtract(timeout);
        return await dbContext.WorkflowInstances
            .Where(x => x.Status == WorkflowStatus.Running && x.StartedOn < cutoffTime)
            .OrderBy(x => x.StartedOn)
            .Take(maxResults)
            .Include(x => x.WorkflowDefinition)
            .ToListAsync(cancellationToken);
    }
}