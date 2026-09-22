namespace Appraisal.Application.Features.PricingAnalysis.SaveIncomeAnalysis;

/// <summary>
/// DisplaySeq must be unique among siblings: IncomeCalculationService orders its calculation
/// by it (method 08 reads what 01/02 publish), so a tie falls back to load order — request
/// order on Preview, database order on Save — and the two can disagree on the same input.
/// </summary>
public static class IncomeDisplaySeqValidator
{
    public static void EnsureUnique(IReadOnlyList<IncomeSectionInput> sections)
    {
        EnsureUnique(sections.Select(s => s.DisplaySeq), "sections");
        foreach (var section in sections)
        {
            EnsureUnique(section.Categories.Select(c => c.DisplaySeq), $"categories of section '{section.SectionName}'");
            foreach (var category in section.Categories)
                EnsureUnique(category.Assumptions.Select(a => a.DisplaySeq), $"assumptions of category '{category.CategoryName}'");
        }
    }

    private static void EnsureUnique(IEnumerable<int> seqs, string where)
    {
        var dup = seqs.GroupBy(x => x).FirstOrDefault(g => g.Count() > 1);
        if (dup is not null)
            throw new BadRequestException($"Duplicate DisplaySeq {dup.Key} among {where}.");
    }
}
