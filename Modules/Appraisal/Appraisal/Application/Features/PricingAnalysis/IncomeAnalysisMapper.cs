using System.Text.Json;
using Appraisal.Contracts.Appraisals.Dto.Income;
using Appraisal.Domain.Appraisals.Income;

namespace Appraisal.Application.Features.PricingAnalysis;

/// <summary>
/// Maps <see cref="IncomeAnalysis"/> domain aggregate to <see cref="IncomeAnalysisDto"/>.
/// Kept as a static helper so handlers don't need Mapster/AutoMapper for this complex tree.
/// </summary>
internal static class IncomeAnalysisMapper
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static IncomeAnalysisDto ToDto(IncomeAnalysis analysis)
    {
        return new IncomeAnalysisDto(
            Id: analysis.Id,
            PricingAnalysisMethodId: analysis.PricingAnalysisMethodId,
            TemplateCode: analysis.TemplateCode,
            TemplateName: analysis.TemplateName,
            TotalNumberOfYears: analysis.TotalNumberOfYears,
            TotalNumberOfDayInYear: analysis.TotalNumberOfDayInYear,
            CapitalizeRate: analysis.CapitalizeRate,
            DiscountedRate: analysis.DiscountedRate,
            FinalValue: analysis.FinalValue,
            FinalValueRounded: analysis.FinalValueRounded,
            Sections: analysis.Sections
                .OrderBy(s => s.DisplaySeq)
                .Select(MapSection)
                .ToList(),
            Summary: MapSummary(analysis.Summary)
        );
    }

    private static IncomeSectionDto MapSection(IncomeSection s) =>
        new(
            Id: s.Id,
            SectionType: s.SectionType,
            SectionName: s.SectionName,
            Identifier: s.Identifier,
            DisplaySeq: s.DisplaySeq,
            TotalSectionValues: DeserializeArray(s.TotalSectionValuesJson),
            Categories: s.Categories
                .OrderBy(c => c.DisplaySeq)
                .Select(MapCategory)
                .ToList()
        );

    private static IncomeCategoryDto MapCategory(IncomeCategory c) =>
        new(
            Id: c.Id,
            CategoryType: c.CategoryType,
            CategoryName: c.CategoryName,
            Identifier: c.Identifier,
            DisplaySeq: c.DisplaySeq,
            TotalCategoryValues: DeserializeArray(c.TotalCategoryValuesJson),
            Assumptions: c.Assumptions
                .OrderBy(a => a.DisplaySeq)
                .Select(MapAssumption)
                .ToList()
        );

    private static IncomeAssumptionDto MapAssumption(IncomeAssumption a) =>
        new(
            Id: a.Id,
            AssumptionType: a.AssumptionType,
            AssumptionName: a.AssumptionName,
            Identifier: a.Identifier,
            DisplaySeq: a.DisplaySeq,
            TotalAssumptionValues: DeserializeArray(a.TotalAssumptionValuesJson),
            Method: MapMethod(a.Method)
        );

    private static IncomeMethodDto MapMethod(IncomeMethod m)
    {
        // Pass the raw DetailJson through as a JsonElement so the client
        // receives the exact persisted JSON shape without any round-tripping.
        JsonElement detail;
        try
        {
            detail = JsonSerializer.Deserialize<JsonElement>(m.DetailJson, JsonOpts);
        }
        catch
        {
            detail = JsonSerializer.Deserialize<JsonElement>("{}", JsonOpts);
        }

        return new IncomeMethodDto(
            MethodTypeCode: m.MethodTypeCode,
            Detail: detail,
            TotalMethodValues: DeserializeArray(m.TotalMethodValuesJson)
        );
    }

    private static IncomeSummaryDto MapSummary(IncomeSummary s) =>
        new(
            ContractRentalFee: DeserializeArray(s.ContractRentalFeeJson),
            GrossRevenue: DeserializeArray(s.GrossRevenueJson),
            GrossRevenueProportional: DeserializeArray(s.GrossRevenueProportionalJson),
            TerminalRevenue: DeserializeArray(s.TerminalRevenueJson),
            TotalNet: DeserializeArray(s.TotalNetJson),
            Discount: DeserializeArray(s.DiscountJson),
            PresentValue: DeserializeArray(s.PresentValueJson)
        );

    private static decimal[] DeserializeArray(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return [];
        try
        {
            return JsonSerializer.Deserialize<decimal[]>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
