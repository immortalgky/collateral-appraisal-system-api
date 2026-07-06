namespace Appraisal.Domain;

/// <summary>
/// Shared reordering for template factors. Extracted so ComparativeAnalysisTemplate and
/// MarketComparableTemplate share one implementation instead of duplicating it.
/// </summary>
public static class TemplateFactorOrdering
{
    /// <summary>
    /// Treats the payload as an ordering intent, not literal sequence numbers. Orders factors by
    /// the requested sequence, appends any factors missing from the payload (by current order),
    /// then reassigns a contiguous 1..n. Duplicate or partial payloads still yield a clean set.
    /// </summary>
    public static void Reorder<T>(
        IList<T> factors,
        IEnumerable<(Guid FactorId, int NewSequence)> reorderCommands)
        where T : class, ISequencedTemplateFactor
    {
        var requestedOrder = reorderCommands
            .GroupBy(c => c.FactorId)
            .Select(g => (FactorId: g.Key, NewSequence: g.Last().NewSequence))
            .OrderBy(c => c.NewSequence)
            .Select(c => c.FactorId)
            .ToList();

        var ordered = new List<T>();
        foreach (var factorId in requestedOrder)
        {
            var factor = factors.FirstOrDefault(f => f.FactorId == factorId);
            if (factor is not null && !ordered.Contains(factor))
                ordered.Add(factor);
        }

        foreach (var factor in factors.OrderBy(f => f.DisplaySequence))
        {
            if (!ordered.Contains(factor))
                ordered.Add(factor);
        }

        for (var i = 0; i < ordered.Count; i++)
            ordered[i].UpdateSequence(i + 1);
    }
}
