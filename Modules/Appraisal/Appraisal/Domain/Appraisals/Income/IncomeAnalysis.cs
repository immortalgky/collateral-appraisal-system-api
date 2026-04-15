using System.Text.Json;
using Appraisal.Domain.Services;

namespace Appraisal.Domain.Appraisals.Income;

/// <summary>
/// Income-approach pricing analysis — 1:1 child of PricingAnalysisMethod (MethodType = "Income").
/// Holds the template snapshot, rate inputs, hierarchical section/category/assumption tree,
/// and server-computed summary + final value.
/// </summary>
public class IncomeAnalysis : Entity<Guid>
{
    private readonly List<IncomeSection> _sections = [];

    public IReadOnlyCollection<IncomeSection> Sections => _sections.AsReadOnly();

    /// <summary>FK to PricingAnalysisMethod (1:1).</summary>
    public Guid PricingAnalysisMethodId { get; private set; }

    // Template snapshot — stored at creation time so changes to the master template don't corrupt saved analyses
    public string TemplateCode { get; private set; } = null!;
    public string TemplateName { get; private set; } = null!;

    // Analysis parameters
    public int TotalNumberOfYears { get; private set; }
    public int TotalNumberOfDayInYear { get; private set; } = 365;

    /// <summary>Capitalize rate as a percentage, e.g. 3.0 = 3%.</summary>
    public decimal CapitalizeRate { get; private set; }

    /// <summary>Discount rate as a percentage, e.g. 8.0 = 8%.</summary>
    public decimal DiscountedRate { get; private set; }

    // Server-computed final values
    public decimal? FinalValue { get; private set; }
    public decimal? FinalValueRounded { get; private set; }

    // Summary (owned — year-indexed arrays)
    public IncomeSummary Summary { get; private set; } = IncomeSummary.Empty();

    private IncomeAnalysis()
    {
        // For EF Core
    }

    public static IncomeAnalysis Create(
        Guid pricingAnalysisMethodId,
        string templateCode,
        string templateName,
        int totalNumberOfYears,
        int totalNumberOfDayInYear,
        decimal capitalizeRate,
        decimal discountedRate)
    {
        if (totalNumberOfYears < 1)
            throw new ArgumentException("TotalNumberOfYears must be at least 1", nameof(totalNumberOfYears));

        return new IncomeAnalysis
        {
            //Id = Guid.CreateVersion7(),
            PricingAnalysisMethodId = pricingAnalysisMethodId,
            TemplateCode = templateCode,
            TemplateName = templateName,
            TotalNumberOfYears = totalNumberOfYears,
            TotalNumberOfDayInYear = totalNumberOfDayInYear <= 0 ? 365 : totalNumberOfDayInYear,
            CapitalizeRate = capitalizeRate,
            DiscountedRate = discountedRate
        };
    }

    public void UpdateParameters(
        string templateCode,
        string templateName,
        int totalNumberOfYears,
        int totalNumberOfDayInYear,
        decimal capitalizeRate,
        decimal discountedRate)
    {
        if (totalNumberOfYears < 1)
            throw new ArgumentException("TotalNumberOfYears must be at least 1", nameof(totalNumberOfYears));

        TemplateCode = templateCode;
        TemplateName = templateName;
        TotalNumberOfYears = totalNumberOfYears;
        TotalNumberOfDayInYear = totalNumberOfDayInYear <= 0 ? 365 : totalNumberOfDayInYear;
        CapitalizeRate = capitalizeRate;
        DiscountedRate = discountedRate;
    }

    public void ReplaceSections(IEnumerable<IncomeSection> sections)
    {
        _sections.Clear();
        _sections.AddRange(sections);
    }

    public void SetComputedValues(
        decimal finalValue,
        decimal finalValueRounded,
        IncomeSummary summary)
    {
        FinalValue = finalValue;
        FinalValueRounded = finalValueRounded;
        Summary = summary;
    }

    /// <summary>
    /// Applies a server-computed <see cref="IncomeCalculationResult"/> to the aggregate,
    /// updating all year-indexed JSON columns and the owned summary block.
    /// </summary>
    public void ApplyCalculationResult(IncomeCalculationResult result)
    {
        foreach (var section in _sections)
        {
            if (result.SectionValues.TryGetValue(section.Id, out var sv))
                section.SetComputedValues(SerializeArray(sv));

            foreach (var category in section.Categories)
            {
                if (result.CategoryValues.TryGetValue(category.Id, out var cv))
                    category.SetComputedValues(SerializeArray(cv));

                foreach (var assumption in category.Assumptions)
                {
                    if (result.AssumptionValues.TryGetValue(assumption.Id, out var av)
                        && result.MethodValues.TryGetValue(assumption.Id, out var mv))
                    {
                        assumption.SetComputedValues(SerializeArray(av), SerializeArray(mv));
                    }
                }
            }
        }

        var summary = IncomeSummary.Create(
            contractRentalFeeJson: SerializeArray(result.ContractRentalFee),
            grossRevenueJson: SerializeArray(result.GrossRevenue),
            grossRevenueProportionalJson: SerializeArray(result.GrossRevenueProportional),
            terminalRevenueJson: SerializeArray(result.TerminalRevenue),
            totalNetJson: SerializeArray(result.TotalNet),
            discountJson: SerializeArray(result.Discount),
            presentValueJson: SerializeArray(result.PresentValue));

        SetComputedValues(result.FinalValue, result.FinalValueRounded, summary);
    }

    private static string SerializeArray(decimal[] arr)
        => JsonSerializer.Serialize(arr);
}
