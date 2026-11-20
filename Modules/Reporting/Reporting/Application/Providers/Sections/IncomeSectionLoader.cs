using System.Data;
using System.Globalization;
using System.Text.Json;
using Reporting.Application.Formatting;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// Loads the "วิธีรายได้ (Income — DCF / Direct Capitalization)" section model.
///
/// Data sources (Dapper read-only):
///   Q1  PricingMethodPicker — the Income methods to print (selected + reference).
///   Q2  appraisal.IncomeAnalyses (FK PricingAnalysisMethodId, unique) + PricingFinalValues:
///       template snapshot, parameters, HBU inputs and the Summary_*Json year arrays.
///   Q3  appraisal.IncomeSections → IncomeCategories → IncomeAssumptions ordered by DisplaySeq:
///       names, Total*ValuesJson year arrays, Method_MethodTypeCode / Method_DetailJson.
///
/// Year arrays mirror IncomeCalculationService: line items and GrossRevenue (= NOI) have N entries;
/// TerminalRevenue / TotalNet / Discount / PresentValue have N-1 (year N only feeds the terminal
/// value, which sits at index N-2). Direct capitalization stores single-element arrays.
/// Returns an empty list when no Income method exists for the appraisal.
/// </summary>
internal static class IncomeSectionLoader
{
    private const int YearsPerBlock = 6;

    public static async Task<IReadOnlyList<IncomeSection>> LoadAllAsync(
        IDbConnection connection,
        Guid appraisalId,
        Func<DateTime, DateTime> toApplicationTime,
        CancellationToken ct = default)
    {
        var methodsPerGroup = await PricingMethodPicker.LoadAsync(connection, appraisalId, ["Income"], ct);
        if (methodsPerGroup.Count == 0)
            return [];

        var sections = new List<IncomeSection>(methodsPerGroup.Count);
        foreach (var method in methodsPerGroup)
        {
            var section = await LoadOneAsync(connection, method, toApplicationTime, ct);
            if (section is not null)
                sections.Add(section);
        }

        return sections;
    }

    private static async Task<IncomeSection?> LoadOneAsync(
        IDbConnection connection,
        PickedMethod method,
        Func<DateTime, DateTime> toApplicationTime,
        CancellationToken ct)
    {
        // Phase C: FinalValueRounded/FinalValueAdjust/IndicatedValue live on PricingFinalValues.
        const string headerSql = """
            SELECT
                ia.Id,
                ia.TemplateCode,
                ia.TemplateName,
                ia.TotalNumberOfYears,
                ia.TotalNumberOfDayInYear,
                ia.CapitalizeRate,
                ia.DiscountedRate,
                COALESCE(pfv.FinalValue, 0)  AS FinalValueRounded,
                pfv.FinalValueOverride       AS FinalValueAdjust,
                pfv.IndicatedValue           AS IndicatedValue,
                ia.IsHighestBestUsed,
                ia.HighestBestUsed_AreaRai,
                ia.HighestBestUsed_AreaNgan,
                ia.HighestBestUsed_AreaWa,
                ia.HighestBestUsed_PricePerSqWa,
                ia.Summary_ContractRentalFeeJson,
                ia.Summary_GrossRevenueJson,
                ia.Summary_GrossRevenueProportionalJson,
                ia.Summary_TerminalRevenueJson,
                ia.Summary_TotalNetJson,
                ia.Summary_DiscountJson,
                ia.Summary_PresentValueJson
            FROM appraisal.IncomeAnalyses ia
            LEFT JOIN appraisal.PricingFinalValues pfv
                ON pfv.PricingMethodId = ia.PricingAnalysisMethodId
            WHERE ia.PricingAnalysisMethodId = @MethodId
            """;

        var header = await connection.QuerySingleOrDefaultAsync<HeaderRow>(
            new CommandDefinition(headerSql, new { MethodId = method.PricingMethodId }, cancellationToken: ct));
        if (header is null)
            return null;

        // A gop category has no assumptions, hence the LEFT JOINs.
        const string treeSql = """
            SELECT
                s.Id            AS SectionId,
                s.SectionType,
                s.SectionName,
                s.Identifier    AS SectionIdentifier,
                s.TotalSectionValuesJson,
                c.Id            AS CategoryId,
                c.CategoryType,
                c.CategoryName,
                c.TotalCategoryValuesJson,
                a.Id            AS AssumptionId,
                a.AssumptionName,
                a.TotalAssumptionValuesJson,
                a.Method_MethodTypeCode AS MethodTypeCode,
                a.Method_DetailJson     AS MethodDetailJson
            FROM appraisal.IncomeSections s
            LEFT JOIN appraisal.IncomeCategories c ON c.IncomeSectionId = s.Id
            LEFT JOIN appraisal.IncomeAssumptions a ON a.IncomeCategoryId = c.Id
            WHERE s.IncomeAnalysisId = @AnalysisId
            ORDER BY s.DisplaySeq, s.Id, c.DisplaySeq, c.Id, a.DisplaySeq, a.Id
            """;

        var tree = (await connection.QueryAsync<TreeRow>(
            new CommandDefinition(treeSql, new { AnalysisId = header.Id }, cancellationToken: ct))).ToList();

        var subs = await PricingSubAnalysisLoader.LoadForHostAsync(connection, method.PricingMethodId, toApplicationTime, ct);
        return Build(method, header, tree, subs);
    }

    // ── Pure mapping (unit-tested) ─────────────────────────────────────────────────

    internal static IncomeSection Build(
        PickedMethod method, HeaderRow h, IReadOnlyList<TreeRow> tree, IReadOnlyList<PricingSubAnalysis>? subAnalyses = null)
    {
        var sections = Group(tree);
        var isDirect = (h.TemplateCode ?? "").StartsWith("direct", StringComparison.OrdinalIgnoreCase)
                       || h.TotalNumberOfYears == 1
                       || sections.Any(s => s.Type == "summaryDirect");
        var years = isDirect ? 1 : Math.Max(h.TotalNumberOfYears, 1);

        var gross = Deserialize(h.Summary_GrossRevenueJson);
        var terminal = Deserialize(h.Summary_TerminalRevenueJson);
        var totalNet = Deserialize(h.Summary_TotalNetJson);
        var discount = Deserialize(h.Summary_DiscountJson);
        var pv = Deserialize(h.Summary_PresentValueJson);

        var rows = BuildRows(sections, h, years, isDirect, gross, terminal, totalNet, discount, pv);

        // HBU top-up applies when IsHighestBestUsed = false (SaveIncomeAnalysisCommandHandler.cs).
        decimal? hbuLand = null;
        if (!h.IsHighestBestUsed && h.HighestBestUsed_PricePerSqWa is not null)
        {
            var totalWa = (h.HighestBestUsed_AreaRai ?? 0) * 400m
                        + (h.HighestBestUsed_AreaNgan ?? 0) * 100m
                        + (h.HighestBestUsed_AreaWa ?? 0m);
            hbuLand = totalWa * h.HighestBestUsed_PricePerSqWa.Value;
        }

        // Priority mirrors the save handler: IndicatedValue → FinalValueAdjust + HBU land → FinalValueRounded.
        var effective = h.IndicatedValue
                        ?? (h.FinalValueAdjust.HasValue ? h.FinalValueAdjust.Value + (hbuLand ?? 0m) : h.FinalValueRounded);

        return new IncomeSection
        {
            GroupNumber         = method.GroupNumber,
            GroupName           = method.GroupName,
            Context             = method.Context,
            TemplateName        = h.TemplateName,
            DaysInYear          = h.TotalNumberOfDayInYear,
            IsDirectCapitalization = isDirect,
            TotalNumberOfYears  = h.TotalNumberOfYears,
            CapitalizeRate      = h.CapitalizeRate,
            DiscountedRate      = isDirect ? null : h.DiscountedRate,
            CashFlowBlocks      = Split(rows, years),
            TerminalBaseNoi     = isDirect ? null : At(gross, years - 1),
            TerminalValue       = isDirect || years < 2 ? null : At(terminal, years - 2),
            DirectNoi           = isDirect ? At(totalNet, 0) ?? At(gross, 0) : null,
            PresentValueTotal   = pv.Length == 0 ? null : pv.Sum(v => v ?? 0m),
            Assumptions         = BuildAssumptions(sections),
            FinalValueRounded   = h.FinalValueRounded,
            FinalValueAdjust    = h.FinalValueAdjust,
            IndicatedValue      = h.IndicatedValue,
            EffectiveFinalValue = effective,
            IsHighestBestUsed   = h.IsHighestBestUsed,
            HbuAreaRai          = h.HighestBestUsed_AreaRai,
            HbuAreaNgan         = h.HighestBestUsed_AreaNgan,
            HbuAreaWa           = h.HighestBestUsed_AreaWa,
            HbuPricePerSqWa     = h.HighestBestUsed_PricePerSqWa,
            HbuLandValue        = hbuLand,
            IsValueEdited       = IsEdited(h.IsHighestBestUsed, h.FinalValueRounded, h.FinalValueAdjust, hbuLand, h.IndicatedValue),
            HbuTotalValue       = hbuLand is > 0m && pv.Length > 0 ? pv.Sum(v => v ?? 0m) + hbuLand : null,
            HbuAreaText         = h.HighestBestUsed_AreaRai is null && h.HighestBestUsed_AreaNgan is null && h.HighestBestUsed_AreaWa is null
                ? null
                : ThaiLandAreaFormatter.FormatTotal(h.HighestBestUsed_AreaRai ?? 0, h.HighestBestUsed_AreaNgan ?? 0, h.HighestBestUsed_AreaWa ?? 0m),
            SubAnalyses         = subAnalyses ?? [],
        };
    }

    /// <summary>
    /// DiscountedCashFlowHighestBestUsed.tsx + SummaryValueCard: the computed appraisal is the final value (HBU), or
    /// floor-to-thousand(adjusted value + split land) when HBU = No with a land value, else the adjusted value; the typed
    /// value is "edited" when it is not that figure rounded to the thousand.
    /// </summary>
    internal static bool IsEdited(bool isHbu, decimal? finalValueRounded, decimal? finalValueAdjust, decimal? hbuLand, decimal? indicated)
    {
        if (indicated is null)
            return false;

        var computed = isHbu
            ? finalValueRounded ?? 0m
            : hbuLand is > 0m
                ? Math.Floor(((finalValueAdjust ?? 0m) + hbuLand.Value) / 1000m) * 1000m
                : finalValueAdjust ?? 0m;
        var rounded = Math.Round(computed / 1000m, MidpointRounding.AwayFromZero) * 1000m;
        return Math.Abs(indicated.Value - rounded) >= 0.005m;
    }

    private static List<IncomeCashFlowRow> BuildRows(
        List<Sec> sections, HeaderRow h, int years, bool isDirect,
        decimal?[] gross, decimal?[] terminal, decimal?[] totalNet, decimal?[] discount, decimal?[] pv)
    {
        var rows = new List<IncomeCashFlowRow>();
        IncomeCashFlowRow Values(string label, string kind, decimal?[] v, string fmt = "N2") =>
            new() { Label = label, Kind = kind, Format = fmt, Cells = Enumerable.Range(0, years).Select(i => At(v, i)).ToList() };
        IncomeCashFlowRow Head(string label, string kind) => new() { Label = label, Kind = kind };

        foreach (var s in sections.Where(s => s.Type is not ("summaryDCF" or "summaryDirect")))
        {
            rows.Add(Head(SectionLabel(s), "sec"));
            var lineCats = s.Categories.Count(c => c.Type != "gop");
            foreach (var c in s.Categories)
            {
                if (c.Type == "gop")
                {
                    rows.Add(Values("กำไรขั้นต้นจากการดำเนินงาน (GOP)", "total", Deserialize(c.TotalsJson)));
                    continue;
                }
                rows.Add(Head(c.Name, "cat"));
                foreach (var a in c.Assumptions)
                    rows.Add(Values(a.Name, "item", Deserialize(a.TotalsJson)));
                // One category in the section: its subtotal would repeat the section total / NOI below.
                if (lineCats > 1)
                    rows.Add(Values("รวม " + c.Name, "sub", Deserialize(c.TotalsJson)));
            }
            if (s.Type == "income")
                rows.Add(Values("รวมรายได้", "total", Deserialize(s.TotalsJson)));
        }

        var contract = Deserialize(h.Summary_ContractRentalFeeJson);
        if (contract.Any(v => v is not null and not 0m))
            rows.Add(Values("หัก ค่าเช่าตามสัญญา", "item", contract));

        rows.Add(Values("รายได้สุทธิ (NOI)", "total", gross));

        var proportion = Deserialize(h.Summary_GrossRevenueProportionalJson);
        if (proportion.Any(v => v is not null and not 0m))
            rows.Add(Values("NOI/รายได้รวม (%)", "item", proportion));

        if (isDirect)
            return rows;

        // N-1 discounting years; year N only feeds the terminal value (index N-2).
        var dcf = years - 1;
        decimal?[] Trim(decimal?[] v) => Enumerable.Range(0, years).Select(i => i < dcf ? At(v, i) : null).ToArray();
        var term = new decimal?[years];
        if (dcf >= 1) term[dcf - 1] = At(terminal, dcf - 1);

        rows.Add(Head("คิดลดกระแสเงินสด (DCF)", "sec"));
        rows.Add(Values("มูลค่าซากสุดท้าย (Terminal Value)", "item", term));
        rows.Add(Values("กระแสเงินสดสุทธิ", "item", Trim(totalNet)));
        rows.Add(Values($"ตัวคูณลด @ {h.DiscountedRate?.ToString("N2", CultureInfo.InvariantCulture) ?? "-"} %", "item", Trim(discount), "N6"));
        rows.Add(Values("มูลค่าปัจจุบัน", "total", Trim(pv)));
        return rows;
    }

    /// <summary>Chunks the year columns into blocks of 6, repeating every row (header rows carry no cells).</summary>
    internal static IReadOnlyList<IncomeCashFlowBlock> Split(IReadOnlyList<IncomeCashFlowRow> rows, int years)
    {
        var blocks = new List<IncomeCashFlowBlock>();
        for (var start = 0; start < years; start += YearsPerBlock)
        {
            var n = Math.Min(YearsPerBlock, years - start);
            blocks.Add(new IncomeCashFlowBlock
            {
                Years = Enumerable.Range(start + 1, n).ToList(),
                Rows = rows.Select(r => r.Cells.Count == 0 ? r : new IncomeCashFlowRow
                {
                    Label = r.Label, Kind = r.Kind, Format = r.Format,
                    Cells = r.Cells.Skip(start).Take(n).ToList(),
                }).ToList(),
            });
        }
        return blocks;
    }

    private static string SectionLabel(Sec s) => s.Type switch
    {
        "income" => "รายได้ (Income)",
        "expenses" => "ค่าใช้จ่าย (Expenses)",
        _ => s.Name,
    };

    // ── Assumption table ───────────────────────────────────────────────────────────

    // Thai short names — FE i18n th/pricingAnalysis.json methodTabs.dcf.sheet.methodNames.
    private static readonly Dictionary<string, string> MethodNames = new()
    {
        ["01"] = "รายได้ห้องต่อวัน",
        ["02"] = "รายได้ห้องตามฤดูกาล",
        ["03"] = "รายได้ห้องพร้อมอัตราการเติบโต",
        ["04"] = "รายได้ห้องพร้อมการเติบโตตามอัตราการเข้าพัก",
        ["05"] = "รายได้ค่าเช่าต่อเดือน",
        ["06"] = "รายได้ค่าเช่าต่อตารางเมตร",
        ["07"] = "ต้นทุนห้องตามค่าใช้จ่ายต่อห้องต่อวัน",
        ["08"] = "ค่าอาหารและเครื่องดื่มต่อห้องต่อวัน",
        ["09"] = "เงินเดือนตามตำแหน่งงาน",
        ["10"] = "ภาษีตามขั้นมูลค่าทรัพย์สิน",
        ["11"] = "ดัชนีต้นทุนพลังงาน",
        ["12"] = "สัดส่วนของต้นทุนทดแทนใหม่",
        ["13"] = "สัดส่วน",
        ["14"] = "จำนวนเงินพร้อมอัตราการเติบโต",
    };

    private static List<IncomeAssumptionLine> BuildAssumptions(List<Sec> sections)
    {
        // Method 13 refTarget → name. dbId is what the calculation resolves; clientId ("kind:id" on the FE) is the fallback.
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in sections)
        {
            names[s.Id.ToString()] = s.Type switch { "income" => "รวมรายได้", "expenses" => "รวมค่าใช้จ่าย", _ => "รวม " + s.Name };
            foreach (var c in s.Categories)
            {
                names[c.Id.ToString()] = c.Name;
                foreach (var a in c.Assumptions)
                    names[a.Id.ToString()] = a.Name;
            }
        }

        var lines = new List<IncomeAssumptionLine>();
        foreach (var a in sections.SelectMany(s => s.Categories).SelectMany(c => c.Assumptions))
        {
            var code = a.MethodCode ?? "";
            lines.Add(new IncomeAssumptionLine
            {
                Number = lines.Count + 1,
                Name = a.Name,
                Method = MethodNames.TryGetValue(code, out var mn) ? $"{code} {mn}" : code,
                Summary = Summarize(code, a.DetailJson, names),
            });
        }
        return lines;
    }

    /// <summary>
    /// One-line Thai summary of an assumption's inputs — same wording as the screen's
    /// dcfAssumptionSummary.ts, but any part whose input is empty or zero is left out.
    /// </summary>
    internal static string? Summarize(string code, string? detailJson, IReadOnlyDictionary<string, string> refNames)
    {
        JsonElement d;
        try
        {
            if (string.IsNullOrWhiteSpace(detailJson)) return null;
            d = JsonDocument.Parse(detailJson).RootElement;
            if (d.ValueKind != JsonValueKind.Object) return null;
        }
        catch (JsonException)
        {
            return null;
        }

        var parts = new List<string>();
        void Add(string? p) { if (!string.IsNullOrEmpty(p)) parts.Add(p); }
        string? Amt(string prop, string fmt) => Num(d, prop) is { } v and not 0m ? string.Format(fmt, F(v)) : null;

        var startIn = Num(d, "startIn") ?? 0m;
        var start = startIn > 1 ? $"เริ่มปี {F(startIn)}" : null;
        var growth = Growth(Num(d, "increaseRatePct"), Num(d, "increaseRateYrs"), "%");
        string? Occ()
        {
            if (Num(d, "occupancyRateFirstYearPct") is not { } first || first == 0m) return null;
            var g = Growth(Num(d, "occupancyRatePct"), Num(d, "occupancyRateYrs"), "");
            return $"เข้าพัก {F(first)}%" + (g is null ? "" : " " + g);
        }

        switch (code)
        {
            case "01":
                Add(Amt("sumSaleableArea", "{0} ห้อง")); Add(Amt("avgRoomRate", "ADR {0}")); Add(Occ());
                break;
            case "02":
                Add(Amt("seasonCount", "{0} ฤดูกาล")); Add(Amt("totalSaleableArea", "{0} ห้อง")); Add(Occ());
                break;
            case "03":
            case "14":
                Add(Amt("firstYearAmt", "{0} บ./ปี"));
                break;
            case "04":
                Add(Amt("firstYearAmt", "{0} บ./ปี")); Add(Occ());
                break;
            case "05":
                Add(Amt("sumSaleableArea", "{0} ห้อง")); Add(Amt("sumRoomIncomePerMonth", "{0} บ./เดือน"));
                break;
            case "06":
                Add(Amt("sumSaleableArea", "{0} ตร.ม.")); Add(Amt("avgRentalRatePerMonth", "{0} บ./ตร.ม./ด."));
                Add(Amt("occupancyRateFirstYearPct", "เช่า {0}%"));
                break;
            case "07":
                Add(Amt("sumSaleableArea", "{0} ห้อง")); Add(Amt("sumTotalRoomExpensePerDay", "{0} บ./วัน"));
                break;
            case "08":
                Add(Amt("firstYearAmt", "{0} บ./ห้อง/วัน"));
                break;
            case "09":
                var people = d.TryGetProperty("jobPositionDetails", out var jobs) && jobs.ValueKind == JsonValueKind.Array
                    ? jobs.EnumerateArray().Sum(j => Num(j, "numberOfEmployees") ?? 0m)
                    : 0m;
                if (people != 0m) Add($"{F(people)} คน");
                Add(Amt("sumTotalSalaryPerYear", "{0} บ./ปี"));
                break;
            case "10":
                Add("ภาษีขั้นบันได");
                break;
            case "11":
                Add(Amt("energyCostIndex", "ดัชนี {0} × พื้นที่"));
                break;
            case "12":
                if (Num(d, "proportionPct") is { } p12 and not 0m)
                    Add($"{F(p12)}% ของต้นทุนทดแทนใหม่" + (Num(d, "newReplacementCost") is { } rc and not 0m ? $" {F(rc)} บาท" : ""));
                break;
            case "13":
                if (Num(d, "proportionPct") is { } p13 and not 0m)
                    Add($"{F(p13)}% ของ {RefName(d, refNames)}");
                Add(start);
                return parts.Count == 0 ? null : string.Join(" · ", parts);
            default:
                return null;
        }

        Add(growth);
        Add(start);
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private static string RefName(JsonElement d, IReadOnlyDictionary<string, string> refNames)
    {
        if (!d.TryGetProperty("refTarget", out var t) || t.ValueKind != JsonValueKind.Object)
            return "(ยังไม่เลือก)";
        foreach (var key in new[] { "dbId", "clientId" })
        {
            if (!t.TryGetProperty(key, out var v) || v.ValueKind != JsonValueKind.String) continue;
            var id = v.GetString() ?? "";
            var colon = id.IndexOf(':');
            if (colon >= 0) id = id[(colon + 1)..];
            if (refNames.TryGetValue(id, out var name)) return name;
        }
        return "(ไม่พบรายการอ้างอิง)";
    }

    private static string? Growth(decimal? pct, decimal? every, string unit)
    {
        if (pct is not { } p || p == 0m) return null;
        var yrs = every ?? 0m;
        return yrs > 1 ? $"+{F(p)}{unit} ทุก {F(yrs)} ปี" : $"+{F(p)}{unit}/ปี";
    }

    private static decimal? Num(JsonElement e, string prop) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetDecimal()
            : null;

    private static string F(decimal v) => v.ToString("#,##0.##", CultureInfo.InvariantCulture);

    // ── Helpers ────────────────────────────────────────────────────────────────────

    private static decimal? At(decimal?[] a, int i) => i >= 0 && i < a.Length ? a[i] : null;

    private static List<Sec> Group(IReadOnlyList<TreeRow> tree) =>
        tree.GroupBy(r => r.SectionId).Select(sg =>
        {
            var s0 = sg.First();
            return new Sec(sg.Key, s0.SectionType ?? "", s0.SectionName ?? "", s0.TotalSectionValuesJson,
                sg.Where(r => r.CategoryId is not null).GroupBy(r => r.CategoryId!.Value).Select(cg =>
                {
                    var c0 = cg.First();
                    return new Cat(cg.Key, c0.CategoryType ?? "", c0.CategoryName ?? "", c0.TotalCategoryValuesJson,
                        cg.Where(r => r.AssumptionId is not null)
                          .Select(r => new Asm(r.AssumptionId!.Value, r.AssumptionName ?? "", r.TotalAssumptionValuesJson,
                              r.MethodTypeCode, r.MethodDetailJson))
                          .ToList());
                }).ToList());
        }).ToList();

    private static decimal?[] Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return [];
        try
        {
            var raw = JsonSerializer.Deserialize<JsonElement[]>(json);
            return raw is null
                ? []
                : raw.Select(e => e.ValueKind == JsonValueKind.Number ? e.GetDecimal() : (decimal?)null).ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed record Sec(Guid Id, string Type, string Name, string? TotalsJson, List<Cat> Categories);
    private sealed record Cat(Guid Id, string Type, string Name, string? TotalsJson, List<Asm> Assumptions);
    private sealed record Asm(Guid Id, string Name, string? TotalsJson, string? MethodCode, string? DetailJson);

    // ── Dapper rows ──────────────────────────────────────────────────────────────────

    internal sealed class HeaderRow
    {
        public Guid Id { get; init; }
        public string? TemplateCode { get; init; }
        public string? TemplateName { get; init; }
        public int TotalNumberOfYears { get; init; }
        public int TotalNumberOfDayInYear { get; init; }
        public decimal? CapitalizeRate { get; init; }
        public decimal? DiscountedRate { get; init; }
        public decimal? FinalValueRounded { get; init; }
        public decimal? FinalValueAdjust { get; init; }
        public decimal? IndicatedValue { get; init; }
        public bool IsHighestBestUsed { get; init; } = true;
        public int? HighestBestUsed_AreaRai { get; init; }
        public int? HighestBestUsed_AreaNgan { get; init; }
        public decimal? HighestBestUsed_AreaWa { get; init; }
        public decimal? HighestBestUsed_PricePerSqWa { get; init; }
        public string? Summary_ContractRentalFeeJson { get; init; }
        public string? Summary_GrossRevenueJson { get; init; }
        public string? Summary_GrossRevenueProportionalJson { get; init; }
        public string? Summary_TerminalRevenueJson { get; init; }
        public string? Summary_TotalNetJson { get; init; }
        public string? Summary_DiscountJson { get; init; }
        public string? Summary_PresentValueJson { get; init; }
    }

    internal sealed class TreeRow
    {
        public Guid SectionId { get; init; }
        public string? SectionType { get; init; }
        public string? SectionName { get; init; }
        public string? SectionIdentifier { get; init; }
        public string? TotalSectionValuesJson { get; init; }
        public Guid? CategoryId { get; init; }
        public string? CategoryType { get; init; }
        public string? CategoryName { get; init; }
        public string? TotalCategoryValuesJson { get; init; }
        public Guid? AssumptionId { get; init; }
        public string? AssumptionName { get; init; }
        public string? TotalAssumptionValuesJson { get; init; }
        public string? MethodTypeCode { get; init; }
        public string? MethodDetailJson { get; init; }
    }
}
