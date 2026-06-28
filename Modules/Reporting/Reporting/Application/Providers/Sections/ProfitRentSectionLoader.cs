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
        CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("AppraisalId", appraisalId);

        // ── Q1: Resolve all ProfitRent PricingMethodIds ───────────────────────────
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
               AND pam.MethodType = 'ProfitRent'
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

        var sections = new List<ProfitRentSection>(methodsPerGroup.Count);
        foreach (var method in methodsPerGroup)
        {
            var section = await LoadOneAsync(connection, method, ct);
            if (section is not null)
                sections.Add(section);
        }

        return sections;
    }

    private static async Task<ProfitRentSection?> LoadOneAsync(
        IDbConnection connection,
        MethodRow method,
        CancellationToken ct)
    {
        var mp = new DynamicParameters();
        mp.Add("MethodId", method.PricingMethodId);

        // ── Q2: ProfitRentAnalyses header ──────────────────────────────────────────
        // FK: PricingMethodId (unique). Source: PricingConfiguration.cs:140.
        // W2 fix: EstimatePriceRounded confirmed on ProfitRentAnalysis.cs line 24.
        // Effective value = EstimatePriceRounded ?? FinalValueRounded
        //   (mirrors SaveProfitRentAnalysisCommandHandler.cs:110)
        const string headerSql = """
            SELECT
                pra.Id               AS ProfitRentAnalysisId,
                pra.MarketRentalFeePerSqWa,
                pra.GrowthRateType,
                pra.GrowthRatePercent,
                pra.GrowthIntervalYears,
                pra.DiscountRate,
                pra.TotalPresentValue,
                pra.FinalValueRounded,
                pra.EstimatePriceRounded
            FROM appraisal.ProfitRentAnalyses pra
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

        return new ProfitRentSection
        {
            GroupNumber            = method.GroupNumber,
            GroupName              = method.GroupName,
            MarketRentalFeePerSqWa = header.MarketRentalFeePerSqWa,
            GrowthRateType         = header.GrowthRateType,
            GrowthRatePercent      = header.GrowthRatePercent,
            GrowthIntervalYears    = header.GrowthIntervalYears,
            DiscountRate           = header.DiscountRate,
            TotalPresentValue      = header.TotalPresentValue,
            FinalValueRounded      = header.FinalValueRounded,
            EstimatePriceRounded   = header.EstimatePriceRounded,
            EffectiveFinalValue    = header.EstimatePriceRounded ?? header.FinalValueRounded,
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

    // ── Private DTOs ──────────────────────────────────────────────────────────────

    private sealed record MethodRow(
        Guid    PropertyGroupId,
        int     GroupNumber,
        string? GroupName,
        Guid    PricingMethodId);

    private sealed record ProfitRentHeaderRow(
        Guid     ProfitRentAnalysisId,
        decimal? MarketRentalFeePerSqWa,
        string?  GrowthRateType,
        decimal? GrowthRatePercent,
        int?     GrowthIntervalYears,
        decimal? DiscountRate,
        decimal? TotalPresentValue,
        decimal? FinalValueRounded,
        decimal? EstimatePriceRounded);

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
