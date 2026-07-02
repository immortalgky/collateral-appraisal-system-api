using System.Collections.Concurrent;
using System.Text.Json;

namespace Workflow.Sla.Services;

/// <summary>
/// Derives the set of activities covered by a Stage SLA window by walking the
/// workflow transition graph (BFS, forward edges only) from the window's
/// StartActivityKey to its EndActivityKey.
///
/// "Forward edge" means the transition does NOT represent a routeback (movement=="B")
/// or a cancellation (movement=="C").  This is inferred from the condition string:
/// any transition whose condition contains a decision name in <see cref="BackwardDecisions"/>
/// or <see cref="CancelDecisions"/>, or whose target is a terminal rejection activity,
/// is excluded from the traversal.
///
/// Parsed transitions are cached per <see cref="Guid"/> WorkflowDefinitionId because
/// workflow definitions change only on deployment — a static in-process cache avoids
/// repeated JSON parsing across requests.
/// </summary>
internal static class WorkflowGraphHelper
{
    // Backward / cancel decision tokens. Matched as substrings of the raw condition so we catch every
    // variant, regardless of the keyword the condition tests against:
    //   decision == 'route_back', 'route_back_ext_admin', 'route_back_ext_checker', 'request_more_info',
    //   'reject', 'cancel', AND meetingOutcome == 'route_back' (not a `decision ==` clause).
    private static readonly string[] BackwardTokens =
        { "route_back", "request_more_info", "reject", "cancel" };

    // Terminal activity IDs — transitions into these are never part of a forward window.
    private static readonly HashSet<string> TerminalIds = new(StringComparer.OrdinalIgnoreCase)
        { "workflow-rejected", "workflow-cancelled" };

    // Cached parsed transitions per WorkflowDefinitionId.
    // Value = list of (From, To, Condition) tuples after forward-edge classification is applied.
    private static readonly ConcurrentDictionary<Guid, IReadOnlyList<(string From, string To, string? Condition)>>
        TransitionCache = new();

    /// <summary>
    /// Returns cached parsed transitions for <paramref name="workflowDefinitionId"/>,
    /// parsing <paramref name="jsonDefinition"/> on first call.
    /// </summary>
    public static IReadOnlyList<(string From, string To, string? Condition)> GetOrParseTransitions(
        Guid workflowDefinitionId, string jsonDefinition) =>
        TransitionCache.GetOrAdd(workflowDefinitionId, _ => ParseTransitions(jsonDefinition));

    /// <summary>
    /// Parses the transition list from a workflow definition's <c>JsonDefinition</c> field.
    /// Returns empty list when the JSON is absent or malformed.
    /// </summary>
    public static IReadOnlyList<(string From, string To, string? Condition)> ParseTransitions(
        string jsonDefinition)
    {
        var result = new List<(string, string, string?)>();
        try
        {
            using var doc = JsonDocument.Parse(jsonDefinition);
            // Root is { workflowSchema: { transitions: [...] } }.
            var root = doc.RootElement;
            var schema = root.TryGetProperty("workflowSchema", out var ws) ? ws : root;
            if (!schema.TryGetProperty("transitions", out var transitions))
                return result;

            foreach (var t in transitions.EnumerateArray())
            {
                var from = t.TryGetProperty("from", out var f)  ? f.GetString()  : null;
                var to   = t.TryGetProperty("to",   out var tv) ? tv.GetString() : null;
                var cond = t.TryGetProperty("condition", out var c) ? c.GetString() : null;
                if (from is not null && to is not null)
                    result.Add((from, to, cond));
            }
        }
        catch (JsonException)
        {
            // Malformed JSON — return empty so the caller skips the graph walk gracefully.
        }
        return result;
    }

    /// <summary>
    /// Returns the set of activity IDs on any forward path from <paramref name="start"/>
    /// to <paramref name="end"/> in the given transition graph.
    ///
    /// Algorithm: two-pass BFS.
    /// <list type="number">
    ///   <item>Forward BFS from <paramref name="start"/> — nodes reachable via forward edges.</item>
    ///   <item>Reverse BFS from <paramref name="end"/> on the same forward graph — nodes from which
    ///         <paramref name="end"/> is reachable.</item>
    ///   <item>Intersection = activities that lie on at least one forward path start→end.</item>
    /// </list>
    ///
    /// Returns an empty set when no forward path exists (e.g. unknown activity keys or empty
    /// transition list because no workflow definition is associated with the policy).
    /// </summary>
    public static HashSet<string> GetForwardPathActivityIds(
        IReadOnlyList<(string From, string To, string? Condition)> transitions,
        string start,
        string end)
    {
        if (string.Equals(start, end, StringComparison.OrdinalIgnoreCase))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { start };

        // Build forward-only adjacency (excludes backward + cancel edges).
        var forward = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (from, to, cond) in transitions)
        {
            if (!IsForwardEdge(to, cond)) continue;
            if (!forward.TryGetValue(from, out var neighbors))
                forward[from] = neighbors = [];
            neighbors.Add(to);
        }

        // Pass 1: activities reachable from start.
        var fromStart = BfsReachable(forward, start);

        // Reverse adjacency (over the forward graph only).
        var reverse = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (from, neighbors) in forward)
        {
            foreach (var to in neighbors)
            {
                if (!reverse.TryGetValue(to, out var revNeighbors))
                    reverse[to] = revNeighbors = [];
                revNeighbors.Add(from);
            }
        }

        // Pass 2: activities from which `end` is reachable (reverse BFS from end).
        var fromEnd = BfsReachable(reverse, end);

        // Intersection — activities on at least one forward path start → end.
        fromStart.IntersectWith(fromEnd);
        return fromStart;
    }

    private static HashSet<string> BfsReachable(
        Dictionary<string, List<string>> adjacency, string source)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { source };
        var queue = new Queue<string>();
        queue.Enqueue(source);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (!adjacency.TryGetValue(node, out var neighbors)) continue;
            foreach (var n in neighbors.Where(visited.Add))
                queue.Enqueue(n);
        }
        return visited;
    }

    private static bool IsForwardEdge(string to, string? condition)
    {
        // Never traverse into terminal rejection/cancellation states.
        if (TerminalIds.Contains(to))
            return false;

        // Exclude any backward/cancel edge — substring match catches route_back_ext_*,
        // meetingOutcome == 'route_back', and compound conditions (`... && assignmentType == ...`).
        var c = condition ?? string.Empty;
        foreach (var token in BackwardTokens)
            if (c.Contains(token, StringComparison.OrdinalIgnoreCase))
                return false;

        return true;
    }
}
