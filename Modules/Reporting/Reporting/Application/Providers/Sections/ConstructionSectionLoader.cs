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
                ci.SummaryPreviousValue,
                ci.SummaryCurrentValue,
                ap.SequenceNumber,
                bad.PropertyName AS BuildingName,
                bad.ConstructionLicenseNumber AS LicenseNumber
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
                -- The item's share of the whole building at this round's progress, persisted as
                -- decimal(7,4). The CURRENT rollup below sums this rather than recomputing
                -- ProportionPct x CurrentPct, so it lands on the same figure as every other site,
                -- which all sum the stored column; recomputing keeps full precision and drifts from
                -- them by a fraction of a percentage point across a thirty-item split. The previous
                -- rollup has to recompute — no stored previous-proportion column exists — and every
                -- other site does the same, so the asymmetry is shared.
                wd.CurrentProportionPct,
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

        // ── Reference to the previous book + this round (same rules as the summary page) ──
        // Previous appraisal = Appraisals.PrevAppraisalId; the round falls back to the chain count
        // only for rows created before Appraisals.InspectionNumber existed.
        const string refSql = """
            SELECT prev.AppraisalNumber AS PrevNumber,
                   prev.AppraisalType   AS PrevType,
                   prev.InspectionNumber AS PrevRound,
                   a.InspectionNumber   AS StampedRound
            FROM appraisal.Appraisals a
            LEFT JOIN appraisal.Appraisals prev ON prev.Id = a.PrevAppraisalId
            WHERE a.Id = @AppraisalId;

            WITH chain AS (
                SELECT a.Id, a.AppraisalType, a.PrevAppraisalId
                FROM appraisal.Appraisals a
                WHERE a.Id = (SELECT PrevAppraisalId FROM appraisal.Appraisals WHERE Id = @AppraisalId)
                UNION ALL
                SELECT p.Id, p.AppraisalType, p.PrevAppraisalId
                FROM appraisal.Appraisals p
                JOIN chain c ON p.Id = c.PrevAppraisalId
            )
            SELECT COUNT(*) FROM chain WHERE AppraisalType = 'Progressive';
            """;

        RefRow? refRow;
        int chainCount;
        using (var refMulti = await connection.QueryMultipleAsync(refSql, ciParams))
        {
            refRow = await refMulti.ReadFirstOrDefaultAsync<RefRow>();
            chainCount = await refMulti.ReadFirstAsync<int>();
        }

        var hasPrev = !string.IsNullOrWhiteSpace(refRow?.PrevNumber);
        var priorIsProgressive = hasPrev &&
            string.Equals(refRow!.PrevType, "Progressive", StringComparison.OrdinalIgnoreCase);
        var (referRound, currentRound) = AppraisalSummaryConstructionDataProvider.ResolveRounds(
            priorIsProgressive, refRow?.PrevRound, refRow?.StampedRound, chainCount);

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
                        // Weighted off the entered percentages, not off the money. Dividing
                        // PreviousPropertyValue by ConstructionValue stopped being exact once both
                        // were rounded to whole baht (CA-614), so these rollups drifted from the
                        // Σ(ProportionPct × pct / 100) the summary block prints above this table —
                        // and collapsed to 0.00% outright for a condo unit, whose inspection
                        // carries no value base at all. See ConstructionMoney.
                        PreviousPct  = groupItems.Sum(d => d.ProportionPct * d.PreviousPct / 100m),
                        CurrentPct   = groupItems.Sum(d => d.CurrentProportionPct),
                        PreviousValue = groupItems.Sum(d => d.PreviousPropertyValue),
                        CurrentValue  = groupItems.Sum(d => d.CurrentPropertyValue),
                    };
                }).ToList();

            return new ConstructionBuilding
            {
                BuildingName      = ci.BuildingName,
                LicenseNumber     = AppraisalSummaryCommonLoader.FirstNonBlank(ci.LicenseNumber),
                Groups            = groups,
                TotalValue        = items.Count > 0 ? items.Sum(d => d.ConstructionValue) : null,
                TotalProportionPct = items.Count > 0 ? items.Sum(d => d.ProportionPct)   : null,
                TotalPreviousPct  = items.Count > 0
                    ? groups.Sum(g => g.PreviousPct)
                    : null,
                TotalCurrentPct   = items.Count > 0
                    ? groups.Sum(g => g.CurrentPct)
                    : null,
                // The inspection's stored figures, not the row sums: the current one may be the inspector's override.
                TotalPreviousValue = items.Count > 0 ? ci.SummaryPreviousValue : null,
                TotalCurrentValue  = items.Count > 0 ? ci.SummaryCurrentValue  : null,
            };
        }).ToList();

        return new ConstructionSection
        {
            Buildings = buildings,
            CurrentInspectionRound = currentRound,
            ReferAppraisalBookNumber = hasPrev && !priorIsProgressive ? refRow!.PrevNumber : null,
            ReferConstructionBookNumber = priorIsProgressive ? refRow!.PrevNumber : null,
            ReferInspectionRound = priorIsProgressive ? referRound : null,
        };
    }

    // ── Private flat DTOs for Dapper mapping ─────────────────────────────────────

    private sealed class RefRow
    {
        public string? PrevNumber { get; init; }
        public string? PrevType { get; init; }
        public int? PrevRound { get; init; }
        public int? StampedRound { get; init; }
    }

    private sealed class CiHeaderRow
    {
        public Guid ConstructionInspectionId { get; init; }
        public decimal TotalValue { get; init; }
        public decimal? SummaryPreviousValue { get; init; }
        public decimal? SummaryCurrentValue { get; init; }
        public int SequenceNumber { get; init; }
        public string? BuildingName { get; init; }
        public string? LicenseNumber { get; init; }
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
        public decimal CurrentProportionPct { get; init; }
        public decimal CurrentPropertyValue { get; init; }
    }
}
