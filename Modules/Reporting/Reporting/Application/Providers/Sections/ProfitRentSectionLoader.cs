using System.Data;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// Loads the "วิธีค่าเช่า (Profit Rent)" section model.
///
/// Data sources (Dapper read-only, no EF Core tracking):
///
///   Q1  appraisal.PropertyGroups
///         → appraisal.PricingAnalysis  (AnchorId = pg.Id, SubjectType = 0)
///           → appraisal.PricingAnalysisApproaches
///             → appraisal.PricingAnalysisMethods WHERE MethodType = 'ProfitRent'
///       Groups by pg.Id; takes first pam.Id per group (lowest Id).
///
///   Q2  appraisal.ProfitRentAnalyses  WHERE PricingMethodId = @MethodId
///       FK column confirmed: PricingMethodId (unique)
///       Source: PricingConfiguration.cs line 140
///       Columns confirmed against AddProfitRentAnalysis migration.
///
///   Q3  appraisal.ProfitRentCalculationDetails  WHERE ProfitRentAnalysisId = @AnalysisId
///       FK column confirmed: ProfitRentAnalysisId
///       Source: AddProfitRentAnalysis migration (child table)
///
/// Returns an empty list when no ProfitRent method exists for the appraisal.
/// </summary>
internal static class ProfitRentSectionLoader
{
    public static async Task<IReadOnlyList<ProfitRentSection>> LoadAllAsync(
        IDbConnection connection,
        Guid appraisalId,
        Func<DateTime, DateTime> toApplicationTime,
        CancellationToken ct = default)
    {
        // ── Q1: the methods to print (selected + reference), see PricingMethodPicker ──
        var methodsPerGroup = await PricingMethodPicker.LoadAsync(connection, appraisalId, ["ProfitRent"], ct);
        if (methodsPerGroup.Count == 0)
            return [];

        var sections = new List<ProfitRentSection>(methodsPerGroup.Count);
        foreach (var method in methodsPerGroup)
        {
            var section = await LoadOneAsync(connection, method, toApplicationTime, ct);
            if (section is not null)
                sections.Add(section);
        }

        return sections;
    }

    private static async Task<ProfitRentSection?> LoadOneAsync(
        IDbConnection connection,
        PickedMethod method,
        Func<DateTime, DateTime> toApplicationTime,
        CancellationToken ct)
    {
        var mp = new DynamicParameters();
        mp.Add("MethodId", method.PricingMethodId);

        // ── Q2: ProfitRentAnalyses header ──────────────────────────────────────────
        // FK: PricingMethodId (unique). Source: PricingConfiguration.cs:140.
        // EstimatePriceRounded confirmed on ProfitRentAnalysis.cs line 24 — a raw, non-computed
        // echo of whatever the client sent, kept for back-compat.
        // Effective value = MethodValue (what the rollup counts) ?? IndicatedValue (appraiser's
        //   override, PricingFinalValues) ?? EstimatePriceRounded ?? FinalValueRounded — mirrors
        //   SaveProfitRentAnalysisCommandHandler's SyncMethodValueWithIndicatedValue precedence.
        // FinalValueRounded is sourced from PricingFinalValues (Phase C: never lived on
        // ProfitRentAnalyses — mirrors LeaseholdSectionLoader / IncomeSectionLoader).
        const string headerSql = """
            SELECT
                pra.Id               AS ProfitRentAnalysisId,
                pra.MarketRentalFeePerSqWa,
                pra.GrowthRateType,
                pra.GrowthRatePercent,
                pra.GrowthIntervalYears,
                pra.DiscountRate,
                pra.TotalPresentValue,
                pra.TotalMarketRentalFee,
                pra.TotalContractRentalFee,
                pra.TotalReturnsFromLease,
                pra.IncludeBuildingCost,
                COALESCE(pfv.FinalValue, 0) AS FinalValueRounded,
                pra.EstimatePriceRounded,
                pfv.IndicatedValue          AS IndicatedValue,
                pm.MethodValue              AS MethodValue
            FROM appraisal.ProfitRentAnalyses pra
            JOIN appraisal.PricingAnalysisMethods pm
                ON pm.Id = pra.PricingMethodId
            LEFT JOIN appraisal.PricingFinalValues pfv
                ON pfv.PricingMethodId = pra.PricingMethodId
            WHERE pra.PricingMethodId = @MethodId
            """;

        var header = await connection.QuerySingleOrDefaultAsync<ProfitRentHeaderRow>(headerSql, mp);
        if (header is null)
            return null;

        // ── Q3: ProfitRentCalculationDetails ─────────────────────────────────────
        // FK: ProfitRentAnalysisId. Columns confirmed against AddProfitRentAnalysis migration.
        var dp = new DynamicParameters();
        dp.Add("ProfitRentAnalysisId", header.ProfitRentAnalysisId);

        const string detailSql = """
            SELECT
                d.DisplaySequence,
                d.Year,
                d.NumberOfMonths,
                d.MarketRentalFeePerSqWa,
                d.MarketRentalFeeGrowthPercent,
                d.MarketRentalFeePerMonth,
                d.MarketRentalFeePerYear,
                d.ContractRentalFeePerYear,
                d.ReturnsFromLease,
                d.PvFactor,
                d.PresentValue
            FROM appraisal.ProfitRentCalculationDetails d
            WHERE d.ProfitRentAnalysisId = @ProfitRentAnalysisId
            ORDER BY d.DisplaySequence
            """;

        var detailRows = (await connection.QueryAsync<ProfitRentDetailRow>(detailSql, dp)).ToList();

        // ── Q4: growth by period (only printed when the appraiser chose แบบช่วงเวลา) ───
        var periods = LeaseholdSectionLoader.IsPeriod(header.GrowthRateType)
            ? (await connection.QueryAsync<GrowthPeriodRow>(
                "SELECT FromYear, ToYear, GrowthRatePercent FROM appraisal.ProfitRentGrowthPeriods WHERE ProfitRentAnalysisId = @ProfitRentAnalysisId ORDER BY FromYear, ToYear",
                dp)).ToList()
            : [];

        var buildingValue = header.IncludeBuildingCost
            ? await LoadGroupBuildingValueAsync(connection, method.PropertyGroupId, ct)
            : null;
        var effective = header.MethodValue ?? header.IndicatedValue ?? header.EstimatePriceRounded ?? header.FinalValueRounded;
        var (valueWithBuilding, isEdited) = Resolve(header.TotalPresentValue, header.IncludeBuildingCost, buildingValue, effective);

        return new ProfitRentSection
        {
            GroupNumber            = method.GroupNumber,
            GroupName              = method.GroupName,
            Context              = method.Context,
            Leases               = await LeaseAgreementLoader.LoadAsync(connection, method.PropertyGroupId, ct),
            MarketRentalFeePerSqWa = header.MarketRentalFeePerSqWa,
            GrowthRateType         = LeaseAgreementLoader.GrowthTypeThai(header.GrowthRateType),
            GrowthRatePercent      = header.GrowthRatePercent,
            GrowthIntervalYears    = header.GrowthIntervalYears,
            DiscountRate           = header.DiscountRate,
            TotalPresentValue      = header.TotalPresentValue,
            TotalMarketRentalFee   = header.TotalMarketRentalFee,
            TotalContractRentalFee = header.TotalContractRentalFee,
            TotalReturnsFromLease  = header.TotalReturnsFromLease,
            IncludeBuildingCost    = header.IncludeBuildingCost,
            GrowthIsPeriod         = LeaseholdSectionLoader.IsPeriod(header.GrowthRateType),
            GrowthPeriods          = periods,
            MarketRentSources      = await PricingSubAnalysisLoader.LoadForHostAsync(connection, method.PricingMethodId, toApplicationTime, ct),
            FinalValueRounded      = header.FinalValueRounded,
            EstimatePriceRounded   = header.EstimatePriceRounded,
            // MethodValue first: it is exactly what the rollup counts — IndicatedValue when typed, else
            // the computed price INCLUDING the building when IncludeBuildingCost is on. FinalValue on
            // this row is land-only, so without it a building-inclusive PR saved with no override
            // (IndicatedValue now null) printed the land figure only.
            EffectiveFinalValue    = effective,
            BuildingCostValue      = buildingValue,
            ValueIncludingBuilding = valueWithBuilding,
            IsValueEdited          = isEdited,
            TableRows              = detailRows.Select(d => new ProfitRentCalcRow
            {
                DisplaySequence          = d.DisplaySequence,
                Year                     = d.Year,
                NumberOfMonths           = d.NumberOfMonths,
                MarketRentalFeePerSqWa   = d.MarketRentalFeePerSqWa,
                MarketRentalFeeGrowthPercent = d.MarketRentalFeeGrowthPercent,
                MarketRentalFeePerMonth  = d.MarketRentalFeePerMonth,
                MarketRentalFeePerYear   = d.MarketRentalFeePerYear,
                ContractRentalFeePerYear = d.ContractRentalFeePerYear,
                ReturnsFromLease         = d.ReturnsFromLease,
                PvFactor                 = d.PvFactor,
                PresentValue             = d.PresentValue,
            }).ToList(),
        };
    }

    /// <summary>
    /// What the Profit Rent screen compares the used value against: the system figure (PV, or PV + building when the
    /// building is included) rounded to the thousand. A used value that differs is the appraiser's own ("Edited").
    /// </summary>
    internal static (decimal? ValueIncludingBuilding, bool IsEdited) Resolve(
        decimal? totalPresentValue, bool includeBuilding, decimal? buildingValue, decimal? used)
    {
        decimal? withBuilding = includeBuilding && buildingValue is not null
            ? (totalPresentValue ?? 0m) + buildingValue
            : null;
        var computed = includeBuilding ? withBuilding : totalPresentValue;
        if (computed is null || used is null)
            return (withBuilding, false);

        var rounded = Math.Round(computed.Value / 1000m, MidpointRounding.AwayFromZero) * 1000m;
        return (withBuilding, Math.Abs(used.Value - rounded) >= 0.005m);
    }

    /// <summary>
    /// The group's building value after depreciation, the way the screen derives it: the SELECTED Building Cost method's
    /// value when it has one, else the Σ per building of its cost value (the keyed figure, or the depreciation rows rounded
    /// to the thousand). Null when the group has no building.
    /// </summary>
    private static async Task<decimal?> LoadGroupBuildingValueAsync(IDbConnection connection, Guid groupId, CancellationToken ct)
    {
        var selected = await connection.QueryFirstOrDefaultAsync<decimal?>(new CommandDefinition(
            """
            SELECT TOP 1 pm.MethodValue
            FROM appraisal.PricingAnalysis pa
            JOIN appraisal.PricingAnalysisApproaches pap ON pap.PricingAnalysisId = pa.Id
            JOIN appraisal.PricingAnalysisMethods pm ON pm.ApproachId = pap.Id
            WHERE pa.AnchorId = @GroupId AND pa.SubjectType = 0
              AND pm.MethodType = 'BuildingCost' AND pm.IsSelected = 1 AND pm.MethodValue > 0
            ORDER BY pm.Id
            """, new { GroupId = groupId }, cancellationToken: ct));
        if (selected is not null)
            return selected;

        var rows = (await connection.QueryAsync<(Guid BuildingId, decimal? Keyed, decimal? Price)>(new CommandDefinition(
            """
            SELECT bad.Id AS BuildingId, bad.BuildingCostValue AS Keyed, bdd.PriceAfterDepreciation AS Price
            FROM appraisal.PropertyGroupItems pgi
            JOIN appraisal.AppraisalProperties ap ON ap.Id = pgi.AppraisalPropertyId
            JOIN appraisal.BuildingAppraisalDetails bad ON bad.AppraisalPropertyId = ap.Id
            LEFT JOIN appraisal.BuildingDepreciationDetails bdd ON bdd.BuildingAppraisalDetailId = bad.Id
            WHERE pgi.PropertyGroupId = @GroupId
            """, new { GroupId = groupId }, cancellationToken: ct))).ToList();

        return rows.Count == 0
            ? null
            : rows.GroupBy(r => r.BuildingId)
                  .Sum(g => AppraisalSummaryLandBuildingDataProvider.BuildingCostTotal(
                      g.First().Keyed, g.Select(r => r.Price)));
    }

    // ── Private DTOs ──────────────────────────────────────────────────────────────

    private sealed record ProfitRentHeaderRow(
        Guid     ProfitRentAnalysisId,
        decimal? MarketRentalFeePerSqWa,
        string?  GrowthRateType,
        decimal? GrowthRatePercent,
        int?     GrowthIntervalYears,
        decimal? DiscountRate,
        decimal? TotalPresentValue,
        decimal? TotalMarketRentalFee,
        decimal? TotalContractRentalFee,
        decimal? TotalReturnsFromLease,
        bool     IncludeBuildingCost,
        decimal? FinalValueRounded,
        decimal? EstimatePriceRounded,
        decimal? IndicatedValue,
        decimal? MethodValue);   // positional — keep in the SELECT's column order

    private sealed record ProfitRentDetailRow(
        int      DisplaySequence,
        decimal? Year,
        decimal? NumberOfMonths,
        decimal? MarketRentalFeePerSqWa,
        decimal? MarketRentalFeeGrowthPercent,
        decimal? MarketRentalFeePerMonth,
        decimal? MarketRentalFeePerYear,
        decimal? ContractRentalFeePerYear,
        decimal? ReturnsFromLease,
        decimal? PvFactor,
        decimal? PresentValue);
}
