namespace Common.Application.Features.Monitoring.Shared;

/// <summary>
/// Returned by the /grouped endpoint when ?groupBy=pic|company|activity is requested.
/// Only the 4 OLA-bearing tabs support grouped endpoints.
/// </summary>
public record MonitoringGroupedResult(
    IReadOnlyList<MonitoringGroupRow> Groups,
    int Total
);

/// <summary>
/// One aggregated row in a grouped monitoring result.
/// </summary>
/// <param name="Key">Grouping key: user id, company id, or activity id.</param>
/// <param name="Label">Display label: PIC name, company name, or activity id (frontend resolves via MonitoringActivityMap).</param>
/// <param name="Count">Total rows in this group under the active filter.</param>
/// <param name="Breached">Count of rows where OlaVarianceHours &gt; 0.</param>
/// <param name="AtRisk">Count of rows where OlaVarianceHours &lt;= 0 AND remaining &lt;= 25% of target (excluding zero-target rows).</param>
public record MonitoringGroupRow(
    string Key,
    string Label,
    int Count,
    int Breached,
    int AtRisk
);
