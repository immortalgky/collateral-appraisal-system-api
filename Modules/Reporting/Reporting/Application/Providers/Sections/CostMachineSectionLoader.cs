using System.Data;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// Loads the "วิธีต้นทุน (เครื่องจักร)" (Cost Approach – Machinery) section model
/// for the external appraisal report — FSD §2.1.2.11.
///
/// One calculation table is produced per machine <b>property group</b> (FSD: tables split by
/// machine grouping, headed by the group name). Machines with no property group fall into a
/// single ungrouped table (GroupNumber 0).
///
/// Data sources (Dapper read-only, no EF tracking):
///   appraisal.MachineCostItems          — depreciation inputs + FMV per machine.
///   appraisal.MachineryAppraisalDetails  — name / registration / manufacturer / quantity / age.
///   appraisal.AppraisalProperties        — join anchor; carries AppraisalId.
///   appraisal.PropertyGroupItems / PropertyGroups — group bucketing + group name.
///   appraisal.PricingAnalysisMethods (MethodType = 'MachineryCost').
///
/// Derived display columns (not stored): P = (1 − n/N) × C and R = N − n,
/// where N = LifeSpanYears, n = MachineAge, C = ConditionFactor.
///
/// Returns <see langword="null"/> when no MachineryCost item rows exist for the appraisal.
/// </summary>
internal static class CostMachineSectionLoader
{
    /// <summary>
    /// Loads all <see cref="CostMachineSection"/>s for the given <paramref name="appraisalId"/>.
    /// Returns a list of one section (internal splitting is per <see cref="MachineCostGroup"/>).
    /// Returns an empty list when no MachineryCost method / MachineCostItems exist.
    /// </summary>
    /// <param name="connection">An open Dapper <see cref="IDbConnection"/>.</param>
    /// <param name="appraisalId">The appraisal to load cost-machine data for.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<IReadOnlyList<CostMachineSection>> LoadAllAsync(
        IDbConnection connection,
        Guid appraisalId,
        CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("AppraisalId", appraisalId);

        // ── Query: MachineCostItems joined to detail + property group ────────────
        //
        // Join path:
        //   PricingAnalysisMethods (MethodType='MachineryCost')
        //     → MachineCostItems       (PricingMethodId)
        //       → AppraisalProperties  (AppraisalPropertyId; scopes to @AppraisalId)
        //         LEFT JOIN MachineryAppraisalDetails (AppraisalPropertyId)
        //         LEFT JOIN PropertyGroupItems (AppraisalPropertyId)
        //           LEFT JOIN PropertyGroups   (Id) — GroupNumber / GroupName
        //
        // Ordering: group first (COALESCE GroupNumber 0 = ungrouped), then DisplaySequence
        // for a stable printed sequence within each table.
        const string sql = """
            SELECT
                COALESCE(pg.GroupNumber, 0) AS GroupNumber,
                pg.GroupName,
                mci.DisplaySequence,
                mad.MachineName,
                mad.Brand,
                mad.Model,
                mad.Quantity,
                mad.RegistrationNumber,
                mad.Manufacturer,
                mad.ConditionUse,
                mad.YearOfManufacture,
                mad.MachineAge,
                mci.LifeSpanYears,
                mci.ConditionFactor,
                mci.FunctionalObsolescence,
                mci.EconomicObsolescence,
                mci.RcnReplacementCost,
                mci.FairMarketValue,
                mci.MarketDemandAvailable
            FROM appraisal.MachineCostItems mci
            JOIN appraisal.PricingAnalysisMethods pam
                ON pam.Id = mci.PricingMethodId
               AND pam.MethodType = 'MachineryCost'
            JOIN appraisal.AppraisalProperties ap
                ON ap.Id = mci.AppraisalPropertyId
               AND ap.AppraisalId = @AppraisalId
            LEFT JOIN appraisal.MachineryAppraisalDetails mad
                ON mad.AppraisalPropertyId = mci.AppraisalPropertyId
            LEFT JOIN appraisal.PropertyGroupItems pgi
                ON pgi.AppraisalPropertyId = ap.Id
            LEFT JOIN appraisal.PropertyGroups pg
                ON pg.Id = pgi.PropertyGroupId
            ORDER BY COALESCE(pg.GroupNumber, 0), mci.DisplaySequence, ap.SequenceNumber, ap.Id
            """;

        var rawRows = (await connection.QueryAsync<RawRow>(sql, p)).ToList();

        if (rawRows.Count == 0)
            return [];

        // ── Bucket rows into groups (preserving GroupNumber order from the query) ──
        var groups = rawRows
            .GroupBy(r => r.GroupNumber)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var groupRows = g.ToList();

                var rows = groupRows
                    .Select((r, i) => new MachineCostRow
                    {
                        Sequence            = i + 1,
                        Quantity            = r.Quantity,
                        MachineDetail       = ComposeMachineDetail(r.MachineName, r.Brand, r.Model),
                        RegistrationNumber  = r.RegistrationNumber,
                        ManufacturerCountry = r.Manufacturer,
                        ConditionUse        = r.ConditionUse,
                        YearOfUse           = r.YearOfManufacture,
                        LifeSpanN           = r.LifeSpanYears,
                        AgeN                = r.MachineAge,
                        RemainingR          = DeriveRemaining(r.LifeSpanYears, r.MachineAge),
                        ConditionFactorC    = r.ConditionFactor,
                        PhysicalP           = DerivePhysicalP(r.LifeSpanYears, r.MachineAge, r.ConditionFactor),
                        FunctionalF         = r.FunctionalObsolescence,
                        EconomicE           = r.EconomicObsolescence,
                        Rcn                 = r.RcnReplacementCost,
                        Fmv                 = r.FairMarketValue,
                        MarketDemand        = r.MarketDemandAvailable ? "ใช้งานได้" : "ใช้งานไม่ได้"
                    })
                    .ToList();

                return new MachineCostGroup
                {
                    GroupNumber   = g.Key,
                    GroupName     = groupRows
                        .Select(r => r.GroupName)
                        .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)),
                    Rows          = rows,
                    MachineCount  = groupRows.Count,
                    SurveyedCount = groupRows.Sum(r => r.Quantity ?? 0),
                    TotalRcn      = groupRows.Any(r => r.RcnReplacementCost.HasValue)
                        ? groupRows.Sum(r => r.RcnReplacementCost ?? 0m)
                        : (decimal?)null,
                    TotalFmv      = groupRows.Any(r => r.FairMarketValue.HasValue)
                        ? groupRows.Sum(r => r.FairMarketValue ?? 0m)
                        : (decimal?)null
                };
            })
            .ToList();

        // Wrap in a list of 1. MachineCostGroup provides the internal per-group splitting;
        // the outer section always has GroupNumber = 0, GroupName = null.
        return [new CostMachineSection { Groups = groups }];
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    /// <summary>R = N − n. Null when either input is missing.</summary>
    private static decimal? DeriveRemaining(decimal? lifeSpanN, decimal? ageN)
        => lifeSpanN.HasValue && ageN.HasValue ? lifeSpanN.Value - ageN.Value : null;

    /// <summary>
    /// P = (1 − n/N) × C — physical deterioration factor. Null when N is missing/zero
    /// or n is missing (cannot compute the elapsed-life fraction).
    /// </summary>
    private static decimal? DerivePhysicalP(decimal? lifeSpanN, decimal? ageN, decimal conditionC)
    {
        if (!lifeSpanN.HasValue || lifeSpanN.Value == 0m || !ageN.HasValue)
            return null;
        return (1m - ageN.Value / lifeSpanN.Value) * conditionC;
    }

    private static string? ComposeMachineDetail(string? machineName, string? brand, string? model)
    {
        var parts = new[] { machineName, brand, model }
            .Where(s => !string.IsNullOrWhiteSpace(s));
        var composed = string.Join(" ", parts);
        return string.IsNullOrWhiteSpace(composed) ? null : composed;
    }

    // ── Private flat DTO for Dapper mapping ──────────────────────────────────────

    private sealed class RawRow
    {
        public int GroupNumber { get; init; }
        public string? GroupName { get; init; }
        public int DisplaySequence { get; init; }

        // From MachineryAppraisalDetails (LEFT JOIN — may be null)
        public string? MachineName { get; init; }
        public string? Brand { get; init; }
        public string? Model { get; init; }
        public int? Quantity { get; init; }
        public string? RegistrationNumber { get; init; }
        public string? Manufacturer { get; init; }
        public string? ConditionUse { get; init; }
        public int? YearOfManufacture { get; init; }
        public decimal? MachineAge { get; init; }

        // From MachineCostItems
        public decimal? LifeSpanYears { get; init; }
        public decimal ConditionFactor { get; init; }
        public decimal FunctionalObsolescence { get; init; }
        public decimal EconomicObsolescence { get; init; }
        public decimal? RcnReplacementCost { get; init; }
        public decimal? FairMarketValue { get; init; }
        public bool MarketDemandAvailable { get; init; }
    }
}
