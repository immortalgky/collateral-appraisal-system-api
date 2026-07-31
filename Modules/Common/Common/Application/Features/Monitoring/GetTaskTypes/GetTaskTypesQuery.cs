using Shared.CQRS;

namespace Common.Application.Features.Monitoring.GetTaskTypes;

/// <summary>
/// Task-type filter options for a monitoring screen.
/// <paramref name="MonitoringType"/> selects the screen: "Internal" or "External".
/// </summary>
public record GetTaskTypesQuery(string MonitoringType = MonitoringTypes.Internal)
    : IQuery<IReadOnlyList<TaskTypeOption>>;

/// <summary>Allowed values for <see cref="GetTaskTypesQuery.MonitoringType"/>.</summary>
public static class MonitoringTypes
{
    public const string Internal = "Internal";
    public const string External = "External";

    /// <summary>
    /// Maps caller input onto a known value, falling back to Internal. Used instead of passing the
    /// raw string through so an unrecognised value can never reach the query.
    /// </summary>
    public static string Normalize(string? value) =>
        string.Equals(value, External, StringComparison.OrdinalIgnoreCase) ? External : Internal;
}
