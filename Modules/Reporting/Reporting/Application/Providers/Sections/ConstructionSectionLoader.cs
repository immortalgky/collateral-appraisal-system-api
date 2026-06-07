using System.Data;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// Loads the <see cref="ConstructionSection"/> for FSD §2.1.2.6
/// "ตารางรายละเอียดความคืบหน้างานก่อสร้าง".
///
/// Data sourcing (all Dapper read-only, no EF tracking):
///
///   Step 1  appraisal.ConstructionInspections (ci)
///             JOIN appraisal.AppraisalProperties (ap) ON ap.Id = ci.AppraisalPropertyId
///             JOIN appraisal.BuildingAppraisalDetails (bad) ON bad.AppraisalPropertyId = ap.Id
///           WHERE ap.AppraisalId = @appraisalId AND ci.IsFullDetail = 1
///           → BuildingName (bad.PropertyName), ConstructionInspectionId, TotalValue, ap.SequenceNumber
///
///   Step 2  appraisal.ConstructionWorkDetails (wd)
///             LEFT JOIN parameter.ConstructionWorkGroups (cwg) ON cwg.Id = wd.ConstructionWorkGroupId
///           WHERE wd.ConstructionInspectionId IN (@ids)
///           → per-item: WorkItemName, ConstructionValue, ProportionPct, PreviousProgressPct,
///             CurrentProgressPct, PreviousPropertyValue, CurrentPropertyValue, DisplayOrder,
///             ConstructionWorkGroupId, GroupNameTh (cwg.NameTh), GroupDisplayOrder (cwg.DisplayOrder)
///
/// Returns null when the appraisal has no full-detail CI rows.
///
/// Column provenance (verified against EF configs):
///   appraisal.ConstructionInspections     → ConstructionInspectionConfiguration.cs
///   appraisal.ConstructionWorkDetails     → ConstructionInspectionConfiguration.cs (OwnsMany)
///   appraisal.BuildingAppraisalDetails    → BuildingAppraisalDetailConfiguration.cs
///   parameter.ConstructionWorkGroups      → ConstructionWorkGroupConfiguration.cs (schema: parameter)
/// </summary>
internal static class ConstructionSectionLoader
{
    /// <summary>
    /// Loads the <see cref="ConstructionSection"/> for the given appraisal.
    /// Returns <see langword="null"/> if the appraisal has no full-detail CI rows.
    /// </summary>
    public static async Task<ConstructionSection?> LoadAsync(
        IDbConnection connection,
        Guid appraisalId,
        CancellationToken ct = default)
    {
        // ── Step 1: Full-detail CI rows for this appraisal ────────────────────────
        // IsFullDetail = 1  → only rows where the inspector entered per-item breakdowns.
        // BuildingAppraisalDetails is a 1:1 owned entity on AppraisalProperty (one row per
        // building property); we LEFT JOIN so that any rare missing-detail case still appears
        // in the results (BuildingName will just be null).
        const string ciSql = """
            SELECT
                ci.Id            AS ConstructionInspectionId,
                ci.TotalValue    AS TotalValue,
                ap.SequenceNumber,
                bad.PropertyName AS BuildingName
            FROM appraisal.ConstructionInspections ci
            JOIN appraisal.AppraisalProperties ap
                ON ap.Id = ci.AppraisalPropertyId
            LEFT JOIN appraisal.BuildingAppraisalDetails bad
                ON bad.AppraisalPropertyId = ap.Id
            WHERE ap.AppraisalId  = @AppraisalId
              AND ci.IsFullDetail = 1
            ORDER BY ap.SequenceNumber
            """;

        var ciParams = new DynamicParameters();
        ciParams.Add("AppraisalId", appraisalId);

        var ciRows = (await connection.QueryAsync<CiHeaderRow>(ciSql, ciParams)).ToList();
        if (ciRows.Count == 0)
            return null;

        // ── Step 2: Work details for all matching CI rows ─────────────────────────
        // Collect the IDs and run a single query with IN (@ids) via Dapper's list expansion.
        var ciIds = ciRows.Select(r => r.ConstructionInspectionId).ToList();

        const string detailSql = """
            SELECT
                wd.ConstructionInspectionId,
                wd.ConstructionWorkGroupId,
                ISNULL(cwg.NameTh, N'กลุ่มงาน')    AS GroupNameTh,
                ISNULL(cwg.DisplayOrder, 9999)       AS GroupDisplayOrder,
                wd.WorkItemName,
                wd.DisplayOrder,
                wd.ConstructionValue,
                wd.ProportionPct,
                wd.PreviousProgressPct               AS PreviousPct,
                wd.CurrentProgressPct                AS CurrentPct,
                wd.PreviousPropertyValue,
                wd.CurrentPropertyValue
            FROM appraisal.ConstructionWorkDetails wd
            LEFT JOIN parameter.ConstructionWorkGroups cwg
                ON cwg.Id = wd.ConstructionWorkGroupId
            WHERE wd.ConstructionInspectionId IN @CiIds
            ORDER BY
                wd.ConstructionInspectionId,
                ISNULL(cwg.DisplayOrder, 9999),
                wd.DisplayOrder
            """;

        var detailParams = new DynamicParameters();
        detailParams.Add("CiIds", ciIds);

        var detailRows = (await connection.QueryAsync<WorkDetailRow>(detailSql, detailParams)).ToList();

        // ── Assemble the section hierarchy ────────────────────────────────────────
        // Group detail rows by CI ID for fast lookup.
        var detailsByCi = detailRows
            .GroupBy(d => d.ConstructionInspectionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var buildings = ciRows.Select(ci =>
        {
            var items = detailsByCi.TryGetValue(ci.ConstructionInspectionId, out var rows)
                ? rows
                : [];

            // Group work items by their work-group (preserving group display order).
            var groups = items
                .GroupBy(d => d.ConstructionWorkGroupId)
                .OrderBy(g =>
                {
                    var first = g.First();
                    return first.GroupDisplayOrder;
                })
                .Select(g =>
                {
                    var groupItems = g.OrderBy(d => d.DisplayOrder).ToList();
                    var itemRows = groupItems.Select(d => new ConstructionWorkItemRow
                    {
                        ItemName     = d.WorkItemName,
                        Value        = d.ConstructionValue,
                        ProportionPct = d.ProportionPct,
                        PreviousPct  = d.PreviousPct,
                        CurrentPct   = d.CurrentPct,
                        PreviousValue = d.PreviousPropertyValue,
                        CurrentValue  = d.CurrentPropertyValue,
                    }).ToList();

                    return new ConstructionWorkGroupRow
                    {
                        GroupName    = g.First().GroupNameTh,
                        Items        = itemRows,
                        Value        = groupItems.Sum(d => d.ConstructionValue),
                        ProportionPct = groupItems.Sum(d => d.ProportionPct),
                        PreviousPct  = groupItems.Sum(d => d.PreviousPropertyValue > 0 || d.ConstructionValue > 0
                                            ? (d.ConstructionValue > 0
                                                ? d.PreviousPropertyValue / d.ConstructionValue * d.ProportionPct
                                                : 0m)
                                            : 0m),
                        CurrentPct   = groupItems.Sum(d => d.ConstructionValue > 0
                                            ? d.CurrentPropertyValue / d.ConstructionValue * d.ProportionPct
                                            : 0m),
                        PreviousValue = groupItems.Sum(d => d.PreviousPropertyValue),
                        CurrentValue  = groupItems.Sum(d => d.CurrentPropertyValue),
                    };
                }).ToList();

            return new ConstructionBuilding
            {
                BuildingName      = ci.BuildingName,
                Groups            = groups,
                TotalValue        = items.Count > 0 ? items.Sum(d => d.ConstructionValue) : null,
                TotalProportionPct = items.Count > 0 ? items.Sum(d => d.ProportionPct)   : null,
                TotalPreviousPct  = items.Count > 0
                    ? groups.Sum(g => g.PreviousPct)
                    : null,
                TotalCurrentPct   = items.Count > 0
                    ? groups.Sum(g => g.CurrentPct)
                    : null,
                TotalPreviousValue = items.Count > 0 ? items.Sum(d => d.PreviousPropertyValue) : null,
                TotalCurrentValue  = items.Count > 0 ? items.Sum(d => d.CurrentPropertyValue)  : null,
            };
        }).ToList();

        return new ConstructionSection { Buildings = buildings };
    }

    // ── Private flat DTOs for Dapper mapping ─────────────────────────────────────

    private sealed class CiHeaderRow
    {
        public Guid ConstructionInspectionId { get; init; }
        public decimal TotalValue { get; init; }
        public int SequenceNumber { get; init; }
        public string? BuildingName { get; init; }
    }

    private sealed class WorkDetailRow
    {
        public Guid ConstructionInspectionId { get; init; }
        public Guid ConstructionWorkGroupId { get; init; }
        public string GroupNameTh { get; init; } = "กลุ่มงาน";
        public int GroupDisplayOrder { get; init; }
        public string? WorkItemName { get; init; }
        public int DisplayOrder { get; init; }
        public decimal ConstructionValue { get; init; }
        public decimal ProportionPct { get; init; }
        public decimal PreviousPct { get; init; }
        public decimal CurrentPct { get; init; }
        public decimal PreviousPropertyValue { get; init; }
        public decimal CurrentPropertyValue { get; init; }
    }
}
