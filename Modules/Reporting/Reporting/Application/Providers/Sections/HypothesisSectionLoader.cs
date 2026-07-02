using System.Data;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// Loads the "วิธีสมมติฐาน (Hypothesis)" section model.
///
/// Data sources (Dapper read-only, no EF Core tracking):
///
///   Q1  appraisal.PropertyGroups
///         → appraisal.PricingAnalysis  (AnchorId = pg.Id, SubjectType = 0)
///           → appraisal.PricingAnalysisApproaches
///             → appraisal.PricingAnalysisMethods WHERE MethodType = 'Hypothesis'
///       Groups by pg.Id; takes first pam.Id per group (lowest Id).
///
///   Q2  appraisal.HypothesisAnalyses  WHERE PricingMethodId = @MethodId
///       FK column confirmed: PricingMethodId (unique index)
///       Source: HypothesisAnalysisConfiguration.cs line 18
///
///       Owned column names confirmed against HypothesisAnalysisConfiguration.cs:
///         EF auto-names LandBuildingSummary as LandBuildingSummary_{PropertyName}  (lines 27-93)
///         EF auto-names CondominiumSummary  as CondominiumSummary_{PropertyName}   (lines 98-161)
///         No HasColumnName overrides except Remark (lines 93, 161).
///
///       The SELECT uses explicit aliases (LbXxx / CndXxx) so Dapper can map to clean
///       DTO property names — avoids any underscore-stripping ambiguity.
///
/// Returns an empty list when no Hypothesis method exists for the appraisal.
/// </summary>
internal static class HypothesisSectionLoader
{
    public static async Task<IReadOnlyList<HypothesisSection>> LoadAllAsync(
        IDbConnection connection,
        Guid appraisalId,
        CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("AppraisalId", appraisalId);

        // ── Q1: Resolve all Hypothesis PricingMethodIds ──────────────────────────
        const string methodSql = """
            SELECT
                pg.Id          AS PropertyGroupId,
                pg.GroupNumber,
                pg.GroupName,
                pam.Id         AS PricingMethodId
            FROM appraisal.PropertyGroups pg
            JOIN appraisal.PricingAnalysis pa
                ON pa.AnchorId   = pg.Id
               AND pa.SubjectType = 0
            JOIN appraisal.PricingAnalysisApproaches paa
                ON paa.PricingAnalysisId = pa.Id
            JOIN appraisal.PricingAnalysisMethods pam
                ON pam.ApproachId = paa.Id
               AND pam.MethodType = 'Hypothesis'
            WHERE pg.AppraisalId = @AppraisalId
            ORDER BY pg.GroupNumber, pam.Id
            """;

        var allMethodRows = (await connection.QueryAsync<MethodRow>(methodSql, p)).ToList();
        if (allMethodRows.Count == 0)
            return [];

        var methodsPerGroup = allMethodRows
            .GroupBy(r => r.PropertyGroupId)
            .Select(g => g.First())
            .OrderBy(r => r.GroupNumber)
            .ToList();

        var sections = new List<HypothesisSection>(methodsPerGroup.Count);
        foreach (var method in methodsPerGroup)
        {
            var section = await LoadOneAsync(connection, method, ct);
            if (section is not null)
                sections.Add(section);
        }

        return sections;
    }

    private static async Task<HypothesisSection?> LoadOneAsync(
        IDbConnection connection,
        MethodRow method,
        CancellationToken ct)
    {
        var mp = new DynamicParameters();
        mp.Add("MethodId", method.PricingMethodId);

        // ── Q2: HypothesisAnalyses — all summary scalars ──────────────────────────
        // EF owned column names confirmed from HypothesisAnalysisConfiguration.cs:
        //   LandBuildingSummary_{PropertyName} — aliased as Lb{PropertyName}
        //   CondominiumSummary_{PropertyName}  — aliased as Cnd{PropertyName}
        // Explicit aliases avoid Dapper underscore-stripping ambiguity.
        // Variant stored as int (HasConversion<int>): 1=LandBuilding, 2=Condominium.
        const string headerSql = """
            SELECT
                ha.Variant,
                -- LandBuilding summary (FSD C15–C82)
                ha.LandBuildingSummary_TotalRevenue             AS LbTotalRevenue,
                ha.LandBuildingSummary_TotalProjectDevCost      AS LbTotalProjectDevCost,
                ha.LandBuildingSummary_TotalGovTax              AS LbTotalGovTax,
                ha.LandBuildingSummary_RiskPremiumPercent       AS LbRiskPremiumPercent,
                ha.LandBuildingSummary_RiskPremiumAmount        AS LbRiskPremiumAmount,
                ha.LandBuildingSummary_TotalDevCostsAndExpenses AS LbTotalDevCostsAndExpenses,
                ha.LandBuildingSummary_CurrentPropertyValue     AS LbCurrentPropertyValue,
                ha.LandBuildingSummary_DiscountRate             AS LbDiscountRate,
                ha.LandBuildingSummary_DiscountRateFactor       AS LbDiscountRateFactor,
                ha.LandBuildingSummary_FinalPropertyValue       AS LbFinalPropertyValue,
                ha.LandBuildingSummary_TotalAssetValueRounded   AS LbTotalAssetValueRounded,
                ha.LandBuildingSummary_TotalAssetValuePerSqWa   AS LbTotalAssetValuePerSqWa,
                -- Condominium summary (FSD E13–E59)
                ha.CondominiumSummary_TotalRevenue              AS CndTotalRevenue,
                ha.CondominiumSummary_TotalHardCost             AS CndTotalHardCost,
                ha.CondominiumSummary_TotalSoftCost             AS CndTotalSoftCost,
                ha.CondominiumSummary_TotalGovTax               AS CndTotalGovTax,
                ha.CondominiumSummary_RiskProfitPercent         AS CndRiskProfitPercent,
                ha.CondominiumSummary_RiskProfitTotal           AS CndRiskProfitTotal,
                ha.CondominiumSummary_TotalDevCosts             AS CndTotalDevCosts,
                ha.CondominiumSummary_TotalRemainingValue       AS CndTotalRemainingValue,
                ha.CondominiumSummary_DiscountRate              AS CndDiscountRate,
                ha.CondominiumSummary_DiscountRateFactor        AS CndDiscountRateFactor,
                ha.CondominiumSummary_FinalRemainingValue       AS CndFinalRemainingValue,
                ha.CondominiumSummary_TotalAssetValueRounded    AS CndTotalAssetValueRounded,
                ha.CondominiumSummary_TotalAssetValuePerSqM     AS CndTotalAssetValuePerSqM
            FROM appraisal.HypothesisAnalyses ha
            WHERE ha.PricingMethodId = @MethodId
            """;

        var row = await connection.QuerySingleOrDefaultAsync<HypothesisRow>(headerSql, mp);
        if (row is null)
            return null;

        return new HypothesisSection
        {
            GroupNumber               = method.GroupNumber,
            GroupName                 = method.GroupName,
            Variant                   = row.Variant,
            // LandBuilding
            LbTotalRevenue            = row.LbTotalRevenue,
            LbTotalProjectDevCost     = row.LbTotalProjectDevCost,
            LbTotalGovTax             = row.LbTotalGovTax,
            LbRiskPremiumPercent      = row.LbRiskPremiumPercent,
            LbRiskPremiumAmount       = row.LbRiskPremiumAmount,
            LbTotalDevCostsAndExpenses= row.LbTotalDevCostsAndExpenses,
            LbCurrentPropertyValue    = row.LbCurrentPropertyValue,
            LbDiscountRate            = row.LbDiscountRate,
            LbDiscountRateFactor      = row.LbDiscountRateFactor,
            LbFinalPropertyValue      = row.LbFinalPropertyValue,
            LbTotalAssetValueRounded  = row.LbTotalAssetValueRounded,
            LbTotalAssetValuePerSqWa  = row.LbTotalAssetValuePerSqWa,
            // Condominium
            CndTotalRevenue           = row.CndTotalRevenue,
            CndTotalHardCost          = row.CndTotalHardCost,
            CndTotalSoftCost          = row.CndTotalSoftCost,
            CndTotalGovTax            = row.CndTotalGovTax,
            CndRiskProfitPercent      = row.CndRiskProfitPercent,
            CndRiskProfitTotal        = row.CndRiskProfitTotal,
            CndTotalDevCosts          = row.CndTotalDevCosts,
            CndTotalRemainingValue    = row.CndTotalRemainingValue,
            CndDiscountRate           = row.CndDiscountRate,
            CndDiscountRateFactor     = row.CndDiscountRateFactor,
            CndFinalRemainingValue    = row.CndFinalRemainingValue,
            CndTotalAssetValueRounded = row.CndTotalAssetValueRounded,
            CndTotalAssetValuePerSqM  = row.CndTotalAssetValuePerSqM,
        };
    }

    // ── Private DTOs ──────────────────────────────────────────────────────────────

    private sealed record MethodRow(
        Guid    PropertyGroupId,
        int     GroupNumber,
        string? GroupName,
        Guid    PricingMethodId);

    private sealed record HypothesisRow(
        int      Variant,
        // LandBuilding
        decimal? LbTotalRevenue,
        decimal? LbTotalProjectDevCost,
        decimal? LbTotalGovTax,
        decimal? LbRiskPremiumPercent,
        decimal? LbRiskPremiumAmount,
        decimal? LbTotalDevCostsAndExpenses,
        decimal? LbCurrentPropertyValue,
        decimal? LbDiscountRate,
        decimal? LbDiscountRateFactor,
        decimal? LbFinalPropertyValue,
        decimal? LbTotalAssetValueRounded,
        decimal? LbTotalAssetValuePerSqWa,
        // Condominium
        decimal? CndTotalRevenue,
        decimal? CndTotalHardCost,
        decimal? CndTotalSoftCost,
        decimal? CndTotalGovTax,
        decimal? CndRiskProfitPercent,
        decimal? CndRiskProfitTotal,
        decimal? CndTotalDevCosts,
        decimal? CndTotalRemainingValue,
        decimal? CndDiscountRate,
        decimal? CndDiscountRateFactor,
        decimal? CndFinalRemainingValue,
        decimal? CndTotalAssetValueRounded,
        decimal? CndTotalAssetValuePerSqM);
}
