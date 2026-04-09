using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;
using DomainBreakingChange = Workflow.Workflow.Models.BreakingChange;

namespace Workflow.Workflow.Versioning;

public enum InstanceImpactClassification
{
    Safe,
    Unsafe
}

public record InstanceClassification(
    Guid InstanceId,
    string CurrentActivityId,
    DateTime StartedOn,
    InstanceImpactClassification Classification);

public record PublishImpactReport(
    IReadOnlyList<DomainBreakingChange> BreakingChanges,
    string BreakingChangeHash,
    int SafeCount,
    int UnsafeCount,
    IReadOnlyList<InstanceClassification> Sample);

public interface IInstanceImpactAnalyzer
{
    /// <summary>
    /// Classifies each instance as Safe or Unsafe relative to the old/new schema diff.
    /// Unsafe = the instance currently sits on, or can reach, an activity affected by a breaking change.
    /// </summary>
    PublishImpactReport Analyze(
        IReadOnlyList<WorkflowInstance> instances,
        WorkflowSchema oldSchema,
        WorkflowSchema newSchema,
        IReadOnlyList<DomainBreakingChange> breakingChanges);

    /// <summary>
    /// Computes the set of activity ids reachable from the given starting activity by BFS over transitions.
    /// Used by the migration handler to validate that a manual-remap target is reachable from start.
    /// </summary>
    HashSet<string> ComputeReachable(WorkflowSchema schema, string startActivityId);

    /// <summary>
    /// Canonical sha256 hash of the breaking-change list. Used as an optimistic concurrency token
    /// between the preview and confirm publish endpoints.
    /// </summary>
    string ComputeBreakingChangeHash(IReadOnlyList<DomainBreakingChange> changes);
}

public class InstanceImpactAnalyzer : IInstanceImpactAnalyzer
{
    private const int SampleLimit = 50;

    public PublishImpactReport Analyze(
        IReadOnlyList<WorkflowInstance> instances,
        WorkflowSchema oldSchema,
        WorkflowSchema newSchema,
        IReadOnlyList<DomainBreakingChange> breakingChanges)
    {
        if (instances is null) throw new ArgumentNullException(nameof(instances));
        if (oldSchema is null) throw new ArgumentNullException(nameof(oldSchema));
        if (newSchema is null) throw new ArgumentNullException(nameof(newSchema));
        if (breakingChanges is null) throw new ArgumentNullException(nameof(breakingChanges));

        var removedActivityIds = breakingChanges
            .Where(c => c.Type == "ActivityRemoved")
            .Select(c => c.AffectedComponent)
            .ToHashSet();

        var typeChangedActivityIds = breakingChanges
            .Where(c => c.Type == "PropertyChanged" &&
                        c.MigrationData.TryGetValue("PropertyName", out var p) &&
                        p is string ps && ps == "Type")
            .Select(c => c.MigrationData.TryGetValue("ActivityId", out var a) ? a?.ToString() ?? string.Empty : string.Empty)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToHashSet();

        var requiredPropAddedActivityIds = breakingChanges
            .Where(c => c.Type == "PropertyChanged" &&
                        c.MigrationData.TryGetValue("PropertyName", out var p) &&
                        p is string ps && ps != "Type")
            .Select(c => c.MigrationData.TryGetValue("ActivityId", out var a) ? a?.ToString() ?? string.Empty : string.Empty)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToHashSet();

        var transitionRemovedSourceIds = breakingChanges
            .Where(c => c.Type == "TransitionRemoved")
            .Select(c => c.AffectedComponent)
            .ToHashSet();

        var affectedIds = new HashSet<string>();
        affectedIds.UnionWith(removedActivityIds);
        affectedIds.UnionWith(typeChangedActivityIds);
        affectedIds.UnionWith(requiredPropAddedActivityIds);
        affectedIds.UnionWith(transitionRemovedSourceIds);

        var classifications = new List<InstanceClassification>(instances.Count);
        var safe = 0;
        var unsafeCount = 0;

        foreach (var instance in instances)
        {
            var classification = ClassifyInstance(instance, oldSchema, affectedIds);
            if (classification == InstanceImpactClassification.Safe) safe++;
            else unsafeCount++;

            classifications.Add(new InstanceClassification(
                instance.Id,
                instance.CurrentActivityId,
                instance.StartedOn,
                classification));
        }

        var sample = classifications.Take(SampleLimit).ToList();
        var hash = ComputeBreakingChangeHash(breakingChanges);

        return new PublishImpactReport(breakingChanges, hash, safe, unsafeCount, sample);
    }

    private InstanceImpactClassification ClassifyInstance(
        WorkflowInstance instance,
        WorkflowSchema oldSchema,
        HashSet<string> affectedActivityIds)
    {
        if (affectedActivityIds.Count == 0)
            return InstanceImpactClassification.Safe;

        var completed = instance.ActivityExecutions
            .Select(e => e.ActivityId)
            .ToHashSet();

        var reachable = ComputeReachableInternal(oldSchema, instance.CurrentActivityId, completed);

        if (!string.IsNullOrEmpty(instance.CurrentActivityId))
            reachable.Add(instance.CurrentActivityId);

        foreach (var id in reachable)
        {
            if (affectedActivityIds.Contains(id))
                return InstanceImpactClassification.Unsafe;
        }

        return InstanceImpactClassification.Safe;
    }

    public HashSet<string> ComputeReachable(WorkflowSchema schema, string startActivityId)
        => ComputeReachableInternal(schema, startActivityId, skip: null);

    private static HashSet<string> ComputeReachableInternal(
        WorkflowSchema schema,
        string startActivityId,
        HashSet<string>? skip)
    {
        var visited = new HashSet<string>();
        if (string.IsNullOrEmpty(startActivityId)) return visited;

        var transitionsBySource = schema.Transitions
            .GroupBy(t => t.From)
            .ToDictionary(g => g.Key, g => g.Select(t => t.To).ToList());

        var queue = new Queue<string>();
        queue.Enqueue(startActivityId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current)) continue;

            if (!transitionsBySource.TryGetValue(current, out var nexts)) continue;
            foreach (var next in nexts)
            {
                if (visited.Contains(next)) continue;
                if (skip != null && skip.Contains(next)) continue;
                queue.Enqueue(next);
            }
        }

        return visited;
    }

    public string ComputeBreakingChangeHash(IReadOnlyList<DomainBreakingChange> changes)
    {
        if (changes.Count == 0) return string.Empty;

        // Canonicalize: sort by Type then AffectedComponent, emit minimal json.
        var ordered = changes
            .OrderBy(c => c.Type, StringComparer.Ordinal)
            .ThenBy(c => c.AffectedComponent, StringComparer.Ordinal)
            .Select(c => new
            {
                c.Type,
                c.AffectedComponent,
                c.Description,
                Impact = c.Impact.ToString()
            })
            .ToList();

        var json = JsonSerializer.Serialize(ordered);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
