using System.Data;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// Loads the "วิธีต้นทุน (เครื่องจักร)" (Cost Approach – Machinery) section model
/// for the external appraisal report — FSD §2.1.2.11.
///
/// Data sources (Dapper read-only, no EF tracking):
///   appraisal.MachineCostItems      — depreciation inputs + FMV per machine.
///   appraisal.MachineryAppraisalDetails — machine name / registration / age data.
///   appraisal.AppraisalProperties   — join anchor; carries AppraisalId.
///   appraisal.PricingAnalysisMethods (MethodType = 'MachineryCost')
///   appraisal.PricingAnalysisApproaches
///   appraisal.PricingAnalysis       — top of the ownership chain; scoped to AppraisalId
///                                     via AnchorId (SubjectType = 'AppraisalProperty').
///
/// Column sourcing notes:
///   RcnReplacementCost (decimal 18,2)        → MachineCostRow.Rcn
///   LifeSpanYears (decimal 5,1)              → MachineCostRow.LifeSpanN
///   ConditionFactor (decimal 5,2, NOT NULL)  → MachineCostRow.ConditionFactorC
///   FunctionalObsolescence (decimal 5,2)     → MachineCostRow.FunctionalF
///   EconomicObsolescence (decimal 5,2)       → MachineCostRow.EconomicE
///   FairMarketValue (decimal 18,2)           → MachineCostRow.Fmv
///   MarketDemandAvailable (bit)              → MachineCostRow.MarketDemand ("มี"/"ไม่มี")
///   MachineryAppraisalDetails.MachineName    → MachineDetail (composed with Brand + Model)
///   MachineryAppraisalDetails.RegistrationNumber → MachineCostRow.RegistrationNumber
///   MachineryAppraisalDetails.ConditionUse   → MachineCostRow.ConditionUse
///   MachineryAppraisalDetails.YearOfManufacture → MachineCostRow.YearOfUse
///   MachineryAppraisalDetails.MachineAge     → MachineCostRow.AgeN
///
/// Deferred (no source column):
///   Country       — no Country column on MachineryAppraisalDetails; always null.
///   RemainingR    — not stored; R = N - n; always null (template may derive if both present).
///
/// Returns <see langword="null"/> when:
///   - No PricingAnalysis with a MachineryCost method exists for the appraisal, OR
///   - The method exists but has no MachineCostItems.
/// </summary>
internal static class CostMachineSectionLoader
{
    /// <summary>
    /// Loads the <see cref="CostMachineSection"/> for the given <paramref name="appraisalId"/>.
    /// Returns <see langword="null"/> when no MachineryCost method / MachineCostItems exist.
    /// </summary>
    /// <param name="connection">An open Dapper <see cref="IDbConnection"/>.</param>
    /// <param name="appraisalId">The appraisal to load cost-machine data for.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<CostMachineSection?> LoadAsync(
        IDbConnection connection,
        Guid appraisalId,
        CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("AppraisalId", appraisalId);

        // ── Query: MachineCostItems joined to MachineryAppraisalDetails ──────────
        //
        // Join path:
        //   PricingAnalysis (AnchorId = AppraisalProperty.Id, SubjectType='AppraisalProperty')
        //     → PricingAnalysisApproaches (PricingAnalysisId)
        //       → PricingAnalysisMethods  (ApproachId, MethodType='MachineryCost')
        //         → MachineCostItems      (PricingMethodId)
        //           LEFT JOIN MachineryAppraisalDetails (AppraisalPropertyId)
        //
        // AppraisalProperties scopes to the target appraisal:
        //   MachineCostItems.AppraisalPropertyId → AppraisalProperties.Id WHERE AppraisalId = @AppraisalId
        //
        // Ordering: MachineCostItems.DisplaySequence ASC for stable printed sequence.
        //
        // Note on MethodType: the domain constant is 'MachineryCost' (confirmed in
        // MachineryCostCalculationService and PricingAnalysisMethodConfiguration).
        const string sql = """
            SELECT
                mci.DisplaySequence,
                mad.MachineName,
                mad.Brand,
                mad.Model,
                mad.RegistrationNumber,
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
            JOIN appraisal.AppraisalProperties ap
                ON ap.Id = mci.AppraisalPropertyId
               AND ap.AppraisalId = @AppraisalId
            LEFT JOIN appraisal.MachineryAppraisalDetails mad
                ON mad.AppraisalPropertyId = mci.AppraisalPropertyId
            JOIN appraisal.PricingAnalysisMethods pam
                ON pam.Id = mci.PricingMethodId
               AND pam.MethodType = 'MachineryCost'
            ORDER BY mci.DisplaySequence
            """;

        var rawRows = (await connection.QueryAsync<RawRow>(sql, p)).ToList();

        if (rawRows.Count == 0)
            return null;

        // ── Build MachineCostRow list (1-based Sequence from DisplaySequence order) ──
        var rows = rawRows
            .Select((r, i) => new MachineCostRow
            {
                Sequence           = i + 1,
                MachineDetail      = ComposeMachineDetail(r.MachineName, r.Brand, r.Model),
                RegistrationNumber = r.RegistrationNumber,
                Country            = null,       // no source — no Country column on MachineryAppraisalDetails
                ConditionUse       = r.ConditionUse,
                YearOfUse          = r.YearOfManufacture,
                LifeSpanN          = r.LifeSpanYears,
                AgeN               = r.MachineAge,
                RemainingR         = null,       // no source — not stored; R = N - n
                ConditionFactorC   = r.ConditionFactor,
                FunctionalF        = r.FunctionalObsolescence,
                EconomicE          = r.EconomicObsolescence,
                Rcn                = r.RcnReplacementCost,
                Fmv                = r.FairMarketValue,
                MarketDemand       = r.MarketDemandAvailable ? "มี" : null
            })
            .ToList();

        // ── Totals — null when every value in the column is null ─────────────────
        var totalRcn = rawRows.Any(r => r.RcnReplacementCost.HasValue)
            ? rawRows.Sum(r => r.RcnReplacementCost ?? 0m)
            : (decimal?)null;

        var totalFmv = rawRows.Any(r => r.FairMarketValue.HasValue)
            ? rawRows.Sum(r => r.FairMarketValue ?? 0m)
            : (decimal?)null;

        return new CostMachineSection
        {
            Rows     = rows,
            TotalRcn = totalRcn,
            TotalFmv = totalFmv
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

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
        public int DisplaySequence { get; init; }

        // From MachineryAppraisalDetails (LEFT JOIN — may be null)
        public string? MachineName { get; init; }
        public string? Brand { get; init; }
        public string? Model { get; init; }
        public string? RegistrationNumber { get; init; }
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
