using System.Data;
using System.Text.Json;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// Loads the "วิธีรายได้ (Income / DCF)" section model.
///
/// Data sources (Dapper read-only, no EF Core tracking):
///
///   Q1  appraisal.PropertyGroups
///         → appraisal.PricingAnalysis  (AnchorId = pg.Id, SubjectType = 0)
///           → appraisal.PricingAnalysisApproaches
///             → appraisal.PricingAnalysisMethods WHERE MethodType = 'Income'
///       Groups by pg.Id; takes first pam.Id per group (lowest Id).
///
///   Q2  appraisal.IncomeAnalyses  WHERE PricingAnalysisMethodId = @MethodId
///       FK column confirmed: PricingAnalysisMethodId (unique index)
///       Source: IncomeAnalysisConfiguration.cs line 15
///       Reads scalar inputs + 5 JSON summary arrays.
///
/// JSON arrays (Summary_*Json, nvarchar(max), default '[]') are deserialized
/// as decimal?[] and zipped by year index (0..TotalNumberOfYears-1) into IncomeYearRow list.
///
/// Returns an empty list when no Income method exists for the appraisal.
/// </summary>
internal static class IncomeSectionLoader
{
    public static async Task<IReadOnlyList<IncomeSection>> LoadAllAsync(
        IDbConnection connection,
        Guid appraisalId,
        CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("AppraisalId", appraisalId);

        // ── Q1: Resolve all Income PricingMethodIds (one per property group) ─────
        // JOIN path: PropertyGroups → PricingAnalysis (AnchorId=pg.Id, SubjectType=0)
        //   → PricingAnalysisApproaches → PricingAnalysisMethods (MethodType='Income')
        // Confirmed: PricingConfiguration.cs — no IsDeleted on these tables in existing loaders.
        // Confirmed: PricingAnalysis table is singular (PricingAnalysisConfiguration.cs:13).
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
               AND pam.MethodType = 'Income'
            WHERE pg.AppraisalId = @AppraisalId
            ORDER BY pg.GroupNumber, pam.Id
            """;

        var allMethodRows = (await connection.QueryAsync<MethodRow>(methodSql, p)).ToList();
        if (allMethodRows.Count == 0)
            return [];

        // Take the first PricingMethodId per PropertyGroup
        var methodsPerGroup = allMethodRows
            .GroupBy(r => r.PropertyGroupId)
            .Select(g => g.First())
            .OrderBy(r => r.GroupNumber)
            .ToList();

        var sections = new List<IncomeSection>(methodsPerGroup.Count);
        foreach (var method in methodsPerGroup)
        {
            var section = await LoadOneAsync(connection, method, ct);
            if (section is not null)
                sections.Add(section);
        }

        return sections;
    }

    private static async Task<IncomeSection?> LoadOneAsync(
        IDbConnection connection,
        MethodRow method,
        CancellationToken ct)
    {
        var mp = new DynamicParameters();
        mp.Add("MethodId", method.PricingMethodId);

        // ── Q2: IncomeAnalyses header + JSON arrays ────────────────────────────
        // FK column: PricingAnalysisMethodId (unique index).
        // Confirmed against IncomeAnalysisConfiguration.cs line 15.
        // JSON columns: Summary_*Json (nvarchar(max), default '[]')
        // Confirmed against IncomeAnalysisConfiguration.cs lines 44-79.
        // HBU columns: HighestBestUsed_AreaRai/Ngan/Wa/PricePerSqWa
        // Confirmed against IncomeAnalysisConfiguration.cs lines 36-39.
        // Phase C: FinalValueRounded/FinalValueAdjust/AppraisalPriceRounded are sourced
        // from PricingFinalValues (dropped from IncomeAnalyses).
        const string headerSql = """
            SELECT
                ia.TotalNumberOfYears,
                ia.CapitalizeRate,
                ia.DiscountedRate,
                COALESCE(pfv.FinalValueRounded, 0)  AS FinalValueRounded,
                pfv.FinalValueAdjusted              AS FinalValueAdjust,
                pfv.AppraisalPrice                  AS AppraisalPriceRounded,
                ia.IsHighestBestUsed,
                ia.HighestBestUsed_AreaRai,
                ia.HighestBestUsed_AreaNgan,
                ia.HighestBestUsed_AreaWa,
                ia.HighestBestUsed_PricePerSqWa,
                ia.Summary_GrossRevenueJson,
                ia.Summary_TerminalRevenueJson,
                ia.Summary_TotalNetJson,
                ia.Summary_DiscountJson,
                ia.Summary_PresentValueJson
            FROM appraisal.IncomeAnalyses ia
            LEFT JOIN appraisal.PricingFinalValues pfv
                ON pfv.PricingMethodId = ia.PricingAnalysisMethodId
            WHERE ia.PricingAnalysisMethodId = @MethodId
            """;

        var row = await connection.QuerySingleOrDefaultAsync<IncomeHeaderRow>(headerSql, mp);
        if (row is null)
            return null;

        // Deserialize JSON arrays — guard against null/empty defaults
        var grossRevenue   = Deserialize(row.Summary_GrossRevenueJson);
        var terminalRevenu = Deserialize(row.Summary_TerminalRevenueJson);
        var totalNet       = Deserialize(row.Summary_TotalNetJson);
        var discount       = Deserialize(row.Summary_DiscountJson);
        var presentValue   = Deserialize(row.Summary_PresentValueJson);

        var yearCount = row.TotalNumberOfYears;
        var yearRows  = new List<IncomeYearRow>(yearCount);
        for (var i = 0; i < yearCount; i++)
        {
            yearRows.Add(new IncomeYearRow
            {
                YearIndex      = i + 1,
                GrossRevenue   = i < grossRevenue.Length   ? grossRevenue[i]   : null,
                TerminalRevenue= i < terminalRevenu.Length ? terminalRevenu[i] : null,
                TotalNet       = i < totalNet.Length       ? totalNet[i]       : null,
                Discount       = i < discount.Length       ? discount[i]       : null,
                PresentValue   = i < presentValue.Length   ? presentValue[i]   : null,
            });
        }

        // ── W1: Compute effective final value — mirror SaveIncomeAnalysisCommandHandler.cs:137-159 ─
        // Priority: AppraisalPriceRounded > 0  →  FinalValueAdjust + HBU land value  →  FinalValueRounded
        decimal? effectiveFinalValue;
        if (row.AppraisalPriceRounded is > 0)
        {
            effectiveFinalValue = row.AppraisalPriceRounded;
        }
        else if (row.FinalValueAdjust.HasValue)
        {
            var hbuValue = 0m;
            if (!row.IsHighestBestUsed) // C1: HBU land value added when IsHighestBestUsed = false
            {
                var totalWa = (row.HighestBestUsed_AreaRai ?? 0m) * 400m
                            + (row.HighestBestUsed_AreaNgan ?? 0m) * 100m
                            + (row.HighestBestUsed_AreaWa ?? 0m);
                hbuValue = totalWa * (row.HighestBestUsed_PricePerSqWa ?? 0m);
            }
            effectiveFinalValue = row.FinalValueAdjust.Value + hbuValue;
        }
        else
        {
            effectiveFinalValue = row.FinalValueRounded;
        }

        return new IncomeSection
        {
            GroupNumber          = method.GroupNumber,
            GroupName            = method.GroupName,
            TotalNumberOfYears   = row.TotalNumberOfYears,
            CapitalizeRate       = row.CapitalizeRate,
            DiscountedRate       = row.DiscountedRate,
            FinalValueRounded    = row.FinalValueRounded,
            FinalValueAdjust     = row.FinalValueAdjust,
            AppraisalPriceRounded= row.AppraisalPriceRounded,
            EffectiveFinalValue  = effectiveFinalValue,
            IsHighestBestUsed    = row.IsHighestBestUsed,
            HbuAreaRai           = row.HighestBestUsed_AreaRai,
            HbuAreaNgan          = row.HighestBestUsed_AreaNgan,
            HbuAreaWa            = row.HighestBestUsed_AreaWa,
            HbuPricePerSqWa      = row.HighestBestUsed_PricePerSqWa,
            YearRows             = yearRows,
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static decimal?[] Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return [];

        try
        {
            // JSON array may contain nulls (null → null decimal)
            var raw = JsonSerializer.Deserialize<JsonElement[]>(json);
            if (raw is null) return [];

            return raw.Select(e =>
                e.ValueKind == JsonValueKind.Null
                    ? (decimal?)null
                    : (decimal?)e.GetDecimal()).ToArray();
        }
        catch
        {
            return [];
        }
    }

    // ── Private DTOs ──────────────────────────────────────────────────────────────

    private sealed record MethodRow(
        Guid   PropertyGroupId,
        int    GroupNumber,
        string? GroupName,
        Guid   PricingMethodId);

    private sealed record IncomeHeaderRow(
        int      TotalNumberOfYears,
        decimal? CapitalizeRate,
        decimal? DiscountedRate,
        decimal? FinalValueRounded,
        decimal? FinalValueAdjust,
        decimal? AppraisalPriceRounded,
        bool     IsHighestBestUsed,
        decimal? HighestBestUsed_AreaRai,
        decimal? HighestBestUsed_AreaNgan,
        decimal? HighestBestUsed_AreaWa,
        decimal? HighestBestUsed_PricePerSqWa,
        string?  Summary_GrossRevenueJson,
        string?  Summary_TerminalRevenueJson,
        string?  Summary_TotalNetJson,
        string?  Summary_DiscountJson,
        string?  Summary_PresentValueJson);
}
