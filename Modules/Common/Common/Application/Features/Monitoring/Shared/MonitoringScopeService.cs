using Shared.Identity;

namespace Common.Application.Features.Monitoring.Shared;

/// <summary>
/// Determines which workflow activity IDs the current user may see on each monitoring screen.
///
/// Logic:
///   For Internal/External screens the user holds one or more layer permissions
///   (e.g. "MONITORING:PENDING_INTERNAL:CHECKER"). The suffix after the screen prefix
///   is the map key in <see cref="MonitoringActivityMap"/>.
///   Each layer is a flat set — ADMIN is NOT special-cased to "see everything"; it has its
///   own activity-ID list like every other layer (currently scoped to the assignment stage).
///   A user who needs cross-layer visibility must be granted multiple layer permissions.
///
///   For the 4 admin-only screens (Quotation, Followup, Evaluation, MeetingFollowup)
///   there are no activity-ID layers — the handler skips this service.
/// </summary>
public class MonitoringScopeService(ICurrentUserService currentUserService)
{
    /// <summary>
    /// Returns the set of activity IDs the current user may see on the Pending Internal screen.
    /// Returns an empty array when the user holds no valid internal monitoring permission.
    /// </summary>
    public string[] GetInternalActivityIds()
        => ResolveActivityIds(MonitoringPermissions.PendingInternalPrefix, MonitoringActivityMap.Internal);

    /// <summary>
    /// Returns the set of activity IDs the current user may see on the Pending External screen.
    /// Returns an empty array when the user holds no valid external monitoring permission.
    /// </summary>
    public string[] GetExternalActivityIds()
        => ResolveActivityIds(MonitoringPermissions.PendingExternalPrefix, MonitoringActivityMap.External);

    private string[] ResolveActivityIds(
        string permissionPrefix,
        IReadOnlyDictionary<string, string[]> activityMap)
    {
        var permissions = currentUserService.Permissions;

        // Collect all layer suffixes the user holds for this screen prefix
        var layers = permissions
            .Where(p => p.StartsWith(permissionPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(p => p[permissionPrefix.Length..])   // e.g. "checker", "admin"
            .ToList();

        if (layers.Count == 0)
            return [];

        // Union of all activity IDs for held layers
        var activityIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in layers)
        {
            if (activityMap.TryGetValue(layer, out var ids))
                foreach (var id in ids)
                    activityIds.Add(id);
        }

        return [.. activityIds];
    }
}
