using System.Data;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// Loads the "รายละเอียดเครื่องจักร" (Machine Details) section model — FSD §2.1.2.7.
///
/// Data sources (Dapper read-only, no EF tracking):
///   QS1  appraisal.MachineryAppraisalSummaries (1:1 AppraisalId)
///        → summary header counts and condition ratings.
///   QS2  appraisal.MachineryAppraisalDetails joined via appraisal.AppraisalProperties
///        and appraisal.PropertyGroupItems
///        → one row per machine, ordered for display.
///
/// Column sources are confirmed against MachineryAppraisalSummaryConfiguration.cs and
/// MachineryAppraisalDetailConfiguration.cs. No HasColumnName overrides exist on either
/// config, so all DB column names match C# property names exactly.
///
/// Returns <see langword="null"/> when neither a summary row nor any machinery detail
/// rows exist for the appraisal, allowing the caller / orchestrator to omit the section.
/// </summary>
internal static class MachineSectionLoader
{
    /// <summary>
    /// Loads the <see cref="MachineSection"/> for the given <paramref name="appraisalId"/>.
    /// Returns <see langword="null"/> when no machinery summary or detail rows exist.
    /// </summary>
    /// <param name="connection">An open Dapper <see cref="IDbConnection"/>.</param>
    /// <param name="appraisalId">The appraisal to load machine details for.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<MachineSection?> LoadAsync(
        IDbConnection connection,
        Guid appraisalId,
        CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("AppraisalId", appraisalId);

        // ── Batch: 2 result sets, single round-trip ───────────────────────────────
        //
        // QS1 (RS01) — MachineryAppraisalSummaries (appraisal-level 1:1).
        // QS2 (RS02) — MachineryAppraisalDetails per machine, ordered by group then sequence.
        // Both statements are parameterised only on @AppraisalId.
        //
        // QS1 column notes (MachineryAppraisalSummaryConfiguration.cs, no HasColumnName):
        //   SurveyedNumber→SurveyedCount, AppraisalNumber→EvaluatedCount (count of
        //   evaluated machines, not the book number), InstalledAndUseCount→InstalledInUseCount,
        //   AppraisalScrapCount→WreckageCount, NotInstalledCount→NotInstalledCount,
        //   Maintenance→MaintenanceCondition, Exterior→ExteriorCondition,
        //   Performance→Efficiency, MarketDemand, Owner→OwnerName,
        //   MachineAddress→MachineLocation, Obligation, Other.
        //   Deferred: CollateralDetailNarrative (no column).
        //
        // QS2 column notes (MachineryAppraisalDetailConfiguration.cs, no HasColumnName):
        //   MachineName, RegistrationNumber, Brand, Model, Series (→แบบ/Type), SerialNo,
        //   Manufacturer, YearOfManufacture, MachineAge, Quantity, Location,
        //   MachineDimensions, EnergyUse, UsagePurpose, Capacity, MachineParts,
        //   ConditionUse, MachineCondition, ReplacementValue, ConditionValue, Remark,
        //   Other, AppraiserOpinion.
        const string batchSql = """
            -- RS01: QS1 — Machinery appraisal summary (appraisal-level, 1:1)
            SELECT
                mas.SurveyedNumber       AS SurveyedCount,
                mas.AppraisalNumber      AS EvaluatedCount,
                mas.InstalledAndUseCount AS InstalledInUseCount,
                mas.AppraisalScrapCount  AS WreckageCount,
                mas.NotInstalledCount    AS NotInstalledCount,
                mas.Maintenance          AS MaintenanceCondition,
                mas.Exterior             AS ExteriorCondition,
                mas.Performance          AS Efficiency,
                mas.MarketDemand         AS MarketDemand,
                mas.Owner                AS OwnerName,
                mas.MachineAddress       AS MachineLocation,
                mas.Obligation           AS Obligation,
                mas.Other                AS Other
            FROM appraisal.MachineryAppraisalSummaries mas
            WHERE mas.AppraisalId = @AppraisalId;

            -- RS02: QS2 — Per-machine detail rows
            -- Ordered by PropertyGroup then SequenceInGroup for stable printed sequence.
            SELECT
                mad.MachineName,
                mad.RegistrationNumber,
                mad.Brand,
                mad.Model,
                mad.Series,
                mad.SerialNo,
                mad.Manufacturer,
                mad.YearOfManufacture,
                mad.MachineAge,
                mad.Quantity,
                mad.Location,
                mad.MachineDimensions,
                mad.EnergyUse,
                mad.UsagePurpose,
                mad.Capacity,
                mad.MachineParts,
                mad.ConditionUse,
                mad.MachineCondition,
                mad.ReplacementValue,
                mad.ConditionValue,
                mad.Remark,
                mad.Other,
                mad.AppraiserOpinion
            FROM appraisal.MachineryAppraisalDetails mad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = mad.AppraisalPropertyId
            LEFT JOIN appraisal.PropertyGroupItems pgi ON pgi.AppraisalPropertyId = ap.Id
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY pgi.PropertyGroupId, pgi.SequenceInGroup, ap.SequenceNumber;
            """;

        SummaryRow? summary;
        List<DetailRow> detailRows;

        using (var multi = await connection.QueryMultipleAsync(batchSql, p))
        {
            // RS01: QS1 — machinery summary
            summary = await multi.ReadFirstOrDefaultAsync<SummaryRow>();

            // RS02: QS2 — per-machine detail rows
            detailRows = (await multi.ReadAsync<DetailRow>()).ToList();
        }

        // Return null when neither summary nor detail data exists (machinery not applicable)
        if (summary is null && detailRows.Count == 0)
            return null;

        // ── Build MachineRow list (1-based Sequence) ─────────────────────────────
        var machines = detailRows
            .Select((r, i) => new MachineRow
            {
                Sequence          = i + 1,
                Quantity          = r.Quantity,
                MachineName       = r.MachineName,
                RegistrationNumber = r.RegistrationNumber,
                Brand             = r.Brand,
                Model             = r.Model,
                Series            = r.Series,
                Type              = r.Series,
                SerialNo          = r.SerialNo,
                Manufacturer      = r.Manufacturer,
                YearOfManufacture = r.YearOfManufacture,
                MachineAge        = r.MachineAge,
                Location          = r.Location,
                MachineDimensions = r.MachineDimensions,
                EnergyUse         = r.EnergyUse,
                UsagePurpose      = r.UsagePurpose,
                Capacity          = r.Capacity,
                MachineParts      = r.MachineParts,
                ConditionUse      = r.ConditionUse,
                MachineCondition  = r.MachineCondition,
                ReplacementValue  = r.ReplacementValue,
                ConditionValue    = r.ConditionValue,
                Remark            = r.Remark,
                Other             = r.Other,
                AppraiserOpinion  = r.AppraiserOpinion
            })
            .ToList();

        // ── Build section ─────────────────────────────────────────────────────────
        return new MachineSection
        {
            SurveyedCount             = summary?.SurveyedCount,
            EvaluatedCount            = summary?.EvaluatedCount,
            InstalledInUseCount       = summary?.InstalledInUseCount,
            WreckageCount             = summary?.WreckageCount,
            NotInstalledCount         = summary?.NotInstalledCount,
            MaintenanceCondition      = summary?.MaintenanceCondition,
            ExteriorCondition         = summary?.ExteriorCondition,
            Efficiency                = summary?.Efficiency,
            MarketDemand              = summary?.MarketDemand,
            OwnerName                 = summary?.OwnerName,
            MachineLocation           = summary?.MachineLocation,
            Obligation                = summary?.Obligation,
            Other                     = summary?.Other,
            CollateralDetailNarrative = null,   // no source — deferred
            Machines                  = machines
        };
    }

    // ── Private flat DTOs for Dapper mapping ─────────────────────────────────────

    private sealed class SummaryRow
    {
        public int? SurveyedCount { get; init; }
        public int? EvaluatedCount { get; init; }
        public int? InstalledInUseCount { get; init; }
        public int? WreckageCount { get; init; }
        public int? NotInstalledCount { get; init; }
        public string? MaintenanceCondition { get; init; }
        public string? ExteriorCondition { get; init; }
        public string? Efficiency { get; init; }
        public string? MarketDemand { get; init; }
        public string? OwnerName { get; init; }
        public string? MachineLocation { get; init; }
        public string? Obligation { get; init; }
        public string? Other { get; init; }
    }

    private sealed class DetailRow
    {
        public string? MachineName { get; init; }
        public string? RegistrationNumber { get; init; }
        public string? Brand { get; init; }
        public string? Model { get; init; }
        public string? Series { get; init; }
        public string? SerialNo { get; init; }
        public string? Manufacturer { get; init; }
        public int? YearOfManufacture { get; init; }
        public decimal? MachineAge { get; init; }
        public int? Quantity { get; init; }
        public string? Location { get; init; }
        public string? MachineDimensions { get; init; }
        public string? EnergyUse { get; init; }
        public string? UsagePurpose { get; init; }
        public string? Capacity { get; init; }
        public string? MachineParts { get; init; }
        public string? ConditionUse { get; init; }
        public string? MachineCondition { get; init; }
        public decimal? ReplacementValue { get; init; }
        public decimal? ConditionValue { get; init; }
        public string? Remark { get; init; }
        public string? Other { get; init; }
        public string? AppraiserOpinion { get; init; }
    }
}
