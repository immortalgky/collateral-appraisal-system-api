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
        Func<DateTime, DateTime> toApplicationTime,
        CancellationToken ct = default)
    {
        // ── Q1: the methods to print (selected + reference), see PricingMethodPicker ──
        var methodsPerGroup = await PricingMethodPicker.LoadAsync(connection, appraisalId, ["Leasehold"], ct);
        if (methodsPerGroup.Count == 0)
            return [];

        var sections = new List<LeaseholdSection>(methodsPerGroup.Count);
        foreach (var method in methodsPerGroup)
        {
            var section = await LoadOneAsync(connection, method, toApplicationTime, ct);
            if (section is not null)
                sections.Add(section);
        }

        return sections;
    }

    private static async Task<LeaseholdSection?> LoadOneAsync(
        IDbConnection connection,
        PickedMethod method,
        Func<DateTime, DateTime> toApplicationTime,
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
                COALESCE(pfv.FinalValue, 0) AS FinalValueRounded,
                la.IsPartialUsage,
                la.PartialRai,
                la.PartialNgan,
                la.PartialWa,
                la.PartialLandArea,
                la.PricePerSqWa,
                la.PartialLandPrice,
                la.EstimateNetPrice,
                la.EstimatePriceRounded,
                pfv.IndicatedValue                              AS IndicatedValue
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

        // ── Q4: growth by period (only printed when the appraiser chose แบบช่วงเวลา) ───
        var periods = IsPeriod(header.LandGrowthRateType)
            ? (await connection.QueryAsync<GrowthPeriodRow>(
                "SELECT FromYear, ToYear, GrowthRatePercent FROM appraisal.LeaseholdLandGrowthPeriods WHERE LeaseholdAnalysisId = @LeaseholdAnalysisId ORDER BY FromYear, ToYear",
                dp)).ToList()
            : [];

        return new LeaseholdSection
        {
            GroupNumber              = method.GroupNumber,
            GroupName                = method.GroupName,
            Context                = method.Context,
            Leases                 = await LeaseAgreementLoader.LoadAsync(connection, method.PropertyGroupId, ct),
            LandValuePerSqWa         = header.LandValuePerSqWa,
            LandGrowthRateType       = LeaseAgreementLoader.GrowthTypeThai(header.LandGrowthRateType),
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
            // effective = IndicatedValue (appraiser's override) ?? EstimatePriceRounded (legacy column,
            // still populated for rows not yet migrated to IndicatedValue — see the DbUp fix script) ??
            // FinalValueRounded. Mirrors SaveLeaseholdAnalysisCommandHandler.cs's
            // SyncMethodValueWithIndicatedValue precedence. Covers both partial and non-partial usage —
            // there's only the one effective-value expression, used everywhere the section prints a
            // final price (including the partial-usage "มูลค่าสิทธิการเช่า (ปัดเศษ)" row).
            EffectiveFinalValue      = header.IndicatedValue ?? header.EstimatePriceRounded ?? header.FinalValueRounded,
            IsPartialUsage           = header.IsPartialUsage,
            PartialRai               = header.PartialRai,
            PartialNgan              = header.PartialNgan,
            PartialWa                = header.PartialWa,
            PartialLandArea          = header.PartialLandArea,
            PricePerSqWa             = header.PricePerSqWa,
            PartialLandPrice         = header.PartialLandPrice,
            EstimateNetPrice         = header.EstimateNetPrice,
            UncoveredLandPrice       = Uncovered(header.IsPartialUsage, header.EstimateNetPrice, header.FinalValueRounded, header.PricePerSqWa)?.Price,
            UncoveredLandArea        = Uncovered(header.IsPartialUsage, header.EstimateNetPrice, header.FinalValueRounded, header.PricePerSqWa)?.Area,
            EstimatePriceRounded     = header.EstimatePriceRounded,
            LandGrowthIsPeriod       = IsPeriod(header.LandGrowthRateType),
            LandGrowthPeriods        = periods,
            LandValueSources         = await PricingSubAnalysisLoader.LoadForHostAsync(connection, method.PricingMethodId, toApplicationTime, ct),
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

    /// <summary>GrowthRateType "Period" = growth by year ranges (the other value is "Frequency").</summary>
    internal static bool IsPeriod(string? growthRateType) =>
        string.Equals(growthRateType?.Trim(), "Period", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Partial land usage: the leasehold estimate is the PV (rounded) PLUS the land the lease does not cover
    /// (LeaseholdCalculationService.CalculatePartialUsage: estimate = final + remaining area × price). Only the
    /// total is stored, so the term is read back as total − PV, and its area as term / price per sq.wa.
    /// Null when the analysis is not a partial usage or lacks the stored total / price.
    /// </summary>
    internal static (decimal Price, decimal? Area)? Uncovered(
        bool isPartialUsage, decimal? estimateNetPrice, decimal? finalValueRounded, decimal? pricePerSqWa)
    {
        if (!isPartialUsage || estimateNetPrice is null || pricePerSqWa is null or 0m)
            return null;

        var price = estimateNetPrice.Value - (finalValueRounded ?? 0m);
        return (price, Math.Round(price / pricePerSqWa.Value, 2));
    }

    // ── Private DTOs ──────────────────────────────────────────────────────────────

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
        decimal? EstimatePriceRounded,
        decimal? IndicatedValue);

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
