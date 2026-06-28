using System.Data;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// Loads the "วิธีสิทธิการเช่า (Leasehold)" section model.
///
/// Data sources (Dapper read-only, no EF Core tracking):
///
///   Q1  appraisal.PropertyGroups
///         → appraisal.PricingAnalysis  (AnchorId = pg.Id, SubjectType = 0)
///           → appraisal.PricingAnalysisApproaches
///             → appraisal.PricingAnalysisMethods WHERE MethodType = 'Leasehold'
///       Groups by pg.Id; takes first pam.Id per group (lowest Id).
///
///   Q2  appraisal.LeaseholdAnalyses  WHERE PricingMethodId = @MethodId
///       FK column confirmed: PricingMethodId (unique)
///       Source: PricingConfiguration.cs line 134
///       Columns confirmed against AddLeaseholdAnalysis migration.
///
///   Q3  appraisal.LeaseholdCalculationDetails  WHERE LeaseholdAnalysisId = @AnalysisId
///       FK column confirmed: LeaseholdAnalysisId
///       Source: AddLeaseholdAnalysis migration (child table)
///
/// Returns an empty list when no Leasehold method exists for the appraisal.
/// </summary>
internal static class LeaseholdSectionLoader
{
    public static async Task<IReadOnlyList<LeaseholdSection>> LoadAllAsync(
        IDbConnection connection,
        Guid appraisalId,
        CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("AppraisalId", appraisalId);

        // ── Q1: Resolve all Leasehold PricingMethodIds ───────────────────────────
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
               AND pam.MethodType = 'Leasehold'
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

        var sections = new List<LeaseholdSection>(methodsPerGroup.Count);
        foreach (var method in methodsPerGroup)
        {
            var section = await LoadOneAsync(connection, method, ct);
            if (section is not null)
                sections.Add(section);
        }

        return sections;
    }

    private static async Task<LeaseholdSection?> LoadOneAsync(
        IDbConnection connection,
        MethodRow method,
        CancellationToken ct)
    {
        var mp = new DynamicParameters();
        mp.Add("MethodId", method.PricingMethodId);

        // ── Q2: LeaseholdAnalyses header ──────────────────────────────────────────
        // FK: PricingMethodId (unique). Source: PricingConfiguration.cs:134.
        // FinalValueRounded is sourced from PricingFinalValues (Phase C: dropped from LeaseholdAnalyses).
        const string headerSql = """
            SELECT
                la.Id                            AS LeaseholdAnalysisId,
                la.LandValuePerSqWa,
                la.LandGrowthRateType,
                la.LandGrowthRatePercent,
                la.LandGrowthIntervalYears,
                la.ConstructionCostIndex,
                la.InitialBuildingValue,
                la.DepreciationRate,
                la.DepreciationIntervalYears,
                la.BuildingCalcStartYear,
                la.DiscountRate,
                la.TotalIncomeOverLeaseTerm,
                la.ValueAtLeaseExpiry,
                COALESCE(pfv.FinalValueRounded, 0) AS FinalValueRounded,
                la.IsPartialUsage,
                la.PartialRai,
                la.PartialNgan,
                la.PartialWa,
                la.PartialLandArea,
                la.PricePerSqWa,
                la.PartialLandPrice,
                la.EstimateNetPrice,
                la.EstimatePriceRounded
            FROM appraisal.LeaseholdAnalyses la
            LEFT JOIN appraisal.PricingFinalValues pfv
                ON pfv.PricingMethodId = la.PricingMethodId
            WHERE la.PricingMethodId = @MethodId
            """;

        var header = await connection.QuerySingleOrDefaultAsync<LeaseholdHeaderRow>(headerSql, mp);
        if (header is null)
            return null;

        // ── Q3: LeaseholdCalculationDetails ──────────────────────────────────────
        // FK: LeaseholdAnalysisId. Columns confirmed against AddLeaseholdAnalysis migration.
        var dp = new DynamicParameters();
        dp.Add("LeaseholdAnalysisId", header.LeaseholdAnalysisId);

        const string detailSql = """
            SELECT
                d.DisplaySequence,
                d.Year,
                d.LandValue,
                d.LandGrowthPercent,
                d.BuildingValue,
                d.DepreciationAmount,
                d.DepreciationPercent,
                d.BuildingAfterDepreciation,
                d.TotalLandAndBuilding,
                d.RentalIncome,
                d.PvFactor,
                d.NetCurrentRentalIncome
            FROM appraisal.LeaseholdCalculationDetails d
            WHERE d.LeaseholdAnalysisId = @LeaseholdAnalysisId
            ORDER BY d.DisplaySequence
            """;

        var detailRows = (await connection.QueryAsync<LeaseholdDetailRow>(detailSql, dp)).ToList();

        return new LeaseholdSection
        {
            GroupNumber              = method.GroupNumber,
            GroupName                = method.GroupName,
            LandValuePerSqWa         = header.LandValuePerSqWa,
            LandGrowthRateType       = header.LandGrowthRateType,
            LandGrowthRatePercent    = header.LandGrowthRatePercent,
            LandGrowthIntervalYears  = header.LandGrowthIntervalYears,
            ConstructionCostIndex    = header.ConstructionCostIndex,
            InitialBuildingValue     = header.InitialBuildingValue,
            DepreciationRate         = header.DepreciationRate,
            DepreciationIntervalYears= header.DepreciationIntervalYears,
            BuildingCalcStartYear    = header.BuildingCalcStartYear,
            DiscountRate             = header.DiscountRate,
            TotalIncomeOverLeaseTerm = header.TotalIncomeOverLeaseTerm,
            ValueAtLeaseExpiry       = header.ValueAtLeaseExpiry,
            FinalValueRounded        = header.FinalValueRounded,
            // W3 fix: effective = EstimatePriceRounded ?? FinalValueRounded for both partial/non-partial
            // Mirrors SaveLeaseholdAnalysisCommandHandler.cs:154-155
            EffectiveFinalValue      = header.EstimatePriceRounded ?? header.FinalValueRounded,
            IsPartialUsage           = header.IsPartialUsage,
            PartialRai               = header.PartialRai,
            PartialNgan              = header.PartialNgan,
            PartialWa                = header.PartialWa,
            PartialLandArea          = header.PartialLandArea,
            PricePerSqWa             = header.PricePerSqWa,
            PartialLandPrice         = header.PartialLandPrice,
            EstimateNetPrice         = header.EstimateNetPrice,
            EstimatePriceRounded     = header.EstimatePriceRounded,
            TableRows                = detailRows.Select(d => new LeaseholdCalcRow
            {
                DisplaySequence          = d.DisplaySequence,
                Year                     = d.Year,
                LandValue                = d.LandValue,
                LandGrowthPercent        = d.LandGrowthPercent,
                BuildingValue            = d.BuildingValue,
                DepreciationAmount       = d.DepreciationAmount,
                DepreciationPercent      = d.DepreciationPercent,
                BuildingAfterDepreciation= d.BuildingAfterDepreciation,
                TotalLandAndBuilding     = d.TotalLandAndBuilding,
                RentalIncome             = d.RentalIncome,
                PvFactor                 = d.PvFactor,
                NetCurrentRentalIncome   = d.NetCurrentRentalIncome,
            }).ToList(),
        };
    }

    // ── Private DTOs ──────────────────────────────────────────────────────────────

    private sealed record MethodRow(
        Guid    PropertyGroupId,
        int     GroupNumber,
        string? GroupName,
        Guid    PricingMethodId);

    private sealed record LeaseholdHeaderRow(
        Guid     LeaseholdAnalysisId,
        decimal? LandValuePerSqWa,
        string?  LandGrowthRateType,
        decimal? LandGrowthRatePercent,
        int?     LandGrowthIntervalYears,
        decimal? ConstructionCostIndex,
        decimal? InitialBuildingValue,
        decimal? DepreciationRate,
        int?     DepreciationIntervalYears,
        int?     BuildingCalcStartYear,
        decimal? DiscountRate,
        decimal? TotalIncomeOverLeaseTerm,
        decimal? ValueAtLeaseExpiry,
        decimal? FinalValueRounded,
        bool     IsPartialUsage,
        decimal? PartialRai,
        decimal? PartialNgan,
        decimal? PartialWa,
        decimal? PartialLandArea,
        decimal? PricePerSqWa,
        decimal? PartialLandPrice,
        decimal? EstimateNetPrice,
        decimal? EstimatePriceRounded);

    private sealed record LeaseholdDetailRow(
        int      DisplaySequence,
        decimal? Year,
        decimal? LandValue,
        decimal? LandGrowthPercent,
        decimal? BuildingValue,
        decimal? DepreciationAmount,
        decimal? DepreciationPercent,
        decimal? BuildingAfterDepreciation,
        decimal? TotalLandAndBuilding,
        decimal? RentalIncome,
        decimal? PvFactor,
        decimal? NetCurrentRentalIncome);
}
