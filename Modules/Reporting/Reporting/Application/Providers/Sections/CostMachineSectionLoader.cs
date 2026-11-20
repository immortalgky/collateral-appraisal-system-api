using System.Data;
using Reporting.Application.Formatting;
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
    /// <param name="currentYearBe">This year in พ.ศ. — the screen's n is this minus the machine's year of manufacture.</param>
    /// <param name="toApplicationTime">UTC → the application's time zone (for the Date factors of the RCN sub-analyses).</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<IReadOnlyList<CostMachineSection>> LoadAllAsync(
        IDbConnection connection,
        Guid appraisalId,
        int currentYearBe,
        Func<DateTime, DateTime> toApplicationTime,
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
                -- The form writes PropertyName and no longer writes MachineName, while rows
                -- created before that carry the name in MachineName only. Resolve here so the
                -- printed name never comes back empty. Same rule as the summary report.
                COALESCE(NULLIF(mad.PropertyName, ''), NULLIF(mad.MachineName, '')) AS MachineName,
                mad.Brand,
                mad.Model,
                mad.Quantity,
                mad.RegistrationNumber,
                mad.Manufacturer,
                mad.ConditionUse,
                mad.IsPriceCertified,
                mad.YearOfManufacture,
                mad.MachineAge,
                mci.LifeSpanYears,
                mci.ConditionFactor,
                mci.FunctionalObsolescence,
                mci.EconomicObsolescence,
                mci.RcnReplacementCost,
                mci.FairMarketValue,
                mci.MarketDemandAvailable,
                mci.Notes,
                mci.AppraisalPropertyId,
                mci.PricingMethodId,
                pam.Remark AS MethodRemark
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

        // สภาพการใช้งาน code -> Thai (01 = ใช้งานอยู่), the same group the machinery page resolves.
        var conditionNames = (await connection.QueryAsync<(string Code, string? Description)>(
                new CommandDefinition(
                    "SELECT [code] AS Code, [description] AS Description FROM parameter.Parameters WHERE [group] = 'ConditionUse' AND [language] = 'TH' AND [isactive] = 1",
                    cancellationToken: ct)))
            .ToDictionary(r => r.Code, r => r.Description, StringComparer.OrdinalIgnoreCase);

        // ONE MachineryCost method per group. The picker can return several for a group (none selected, or a reference approach next
        // to the used one), and their items would print as one merged, doubled table. Take the non-reference method first, then the
        // selected one, then one that carries a value; ties keep the picker's (creation) order. Its approach / role gives the page banner.
        var picked = await PricingMethodPicker.LoadAsync(connection, appraisalId, ["MachineryCost"], ct);
        var contexts = OnePerGroup(picked).ToDictionary(m => m.PricingMethodId, m => m.Context);

        // Only that method's items: an unselected alternative keeps its MachineCostItems rows, and they must not print.
        rawRows = rawRows.Where(r => contexts.ContainsKey(r.PricingMethodId)).ToList();
        if (rawRows.Count == 0)
            return [];

        // ── ที่มาของราคาทดแทนใหม่ (RCN): the MachineryCostRef sub-analyses, anchored to each machine ──
        var rcnSources = await PricingSubAnalysisLoader.LoadForHostsAsync(
            connection, rawRows.Select(r => r.PricingMethodId).Distinct().ToList(), toApplicationTime, ct);

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
                        ConditionUse        = ParameterCodeFormatter.Translate(r.ConditionUse, conditionNames),
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
                        // Derived like the screen (RCN x P x F x E > 0), not the flag saved with the row:
                        // the screen recomputes from the live inputs every time it opens.
                        MarketDemand        = ScreenMarketDemand(r.ConditionUse, r.IsPriceCertified, r.YearOfManufacture,
                                                  r.LifeSpanYears, r.RcnReplacementCost, r.ConditionFactor,
                                                  r.FunctionalObsolescence, r.EconomicObsolescence, currentYearBe)
                                              ? "ใช้งานได้" : "ใช้งานไม่ได้",
                        Notes               = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim()
                    })
                    .ToList();

                // Each machine's RCN analysis, captioned with the machine it priced, in table order.
                var sources = groupRows
                    .Select((r, i) => (r, Seq: i + 1))
                    .SelectMany(x => rcnSources.Where(s => s.AnchorId == x.r.AppraisalPropertyId).Select(s => new PricingSubAnalysis
                    {
                        Heading     = s.Sub.Heading,
                        Caption     = $"ลำดับที่ {x.Seq}" + (rows[x.Seq - 1].MachineDetail is { } d ? $" {d}" : ""),
                        MethodLabel = s.Sub.MethodLabel,
                        Wqs         = s.Sub.Wqs,
                        SaleGrid    = s.Sub.SaleGrid
                    }))
                    .ToList();
                var remarks = groupRows.Select(r => r.MethodRemark?.Trim()).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();

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
                        : (decimal?)null,
                    RcnSources    = sources,
                    Remark        = remarks.Count == 0 ? null : string.Join("\n", remarks)
                };
            })
            .ToList();

        // One section per property group, carrying the group's own number / name and the method's
        // approach context, so the book places it under that group's heading (a single section with
        // GroupNumber 0 printed every group under "ทรัพย์สินที่ไม่ได้จัดกลุ่ม").
        return groups
            .Select(g => new CostMachineSection
            {
                GroupNumber = g.GroupNumber,
                GroupName   = g.GroupName,
                Context     = rawRows.Where(r => r.GroupNumber == g.GroupNumber)
                                     .Select(r => contexts.GetValueOrDefault(r.PricingMethodId))
                                     .FirstOrDefault(c => c is not null),
                Groups      = [g]
            })
            .ToList();
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private const string NotFoundConditionCode = "03";

    /// <summary>
    /// ONE MachineryCost method per group: the non-reference one first, then the selected one, then one that carries a value; ties keep
    /// the picker's (creation) order. Several methods of a group would print as one merged, doubled table.
    /// </summary>
    internal static IEnumerable<PickedMethod> OnePerGroup(IEnumerable<PickedMethod> picked) =>
        picked.GroupBy(m => m.GroupNumber)
            .Select(g => g.OrderBy(m => m.Context.IsReference)
                          .ThenByDescending(m => m.MethodSelected)
                          .ThenByDescending(m => PricingMethodPicker.HasValue(m.MethodValue))
                          .First());

    /// <summary>
    /// The screen's "ความต้องการตลาด" (CostMachineSection.tsx useRowComputedValues): FMV = RCN x P x F x E and the
    /// demand is Y when FMV &gt; 0. n = this year (พ.ศ.) − year of manufacture, R = N − n floored at 5 years
    /// (except for a machine not found, code 03), P = round((1 − (N − R)/N) x C, 2). A locked row — not found,
    /// or the price certification withdrawn — is worth nothing.
    /// </summary>
    internal static bool ScreenMarketDemand(
        string? conditionUse, bool? isPriceCertified, int? yearOfManufacture, decimal? lifeSpan, decimal? rcn,
        decimal conditionFactor, decimal functionalObsolescence, decimal economicObsolescence, int currentYearBe)
    {
        if (conditionUse == NotFoundConditionCode || isPriceCertified == false)
            return false;

        var n = yearOfManufacture is > 0 ? currentYearBe - yearOfManufacture.Value : 0;
        var life = lifeSpan ?? 0m;
        var residual = life - n;
        if (residual < 5m)
            residual = 5m; // conditionUse is not "03" here: that case returned above
        var physical = life != 0m ? Math.Round((1m - (life - residual) / life) * conditionFactor, 2, MidpointRounding.AwayFromZero) : 0m;
        return (rcn ?? 0m) * physical * functionalObsolescence * economicObsolescence > 0m;
    }

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
        public bool? IsPriceCertified { get; init; }
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
        public string? Notes { get; init; }
        public Guid AppraisalPropertyId { get; init; }
        public Guid PricingMethodId { get; init; }
        public string? MethodRemark { get; init; }
    }
}
