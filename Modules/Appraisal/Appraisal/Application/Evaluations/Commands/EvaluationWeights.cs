namespace Appraisal.Application.Evaluations.Commands;

/// <summary>
/// Loads the per-slot evaluation criteria weights for an appraisal's banking segment.
/// Mirrors the segment resolution in vw_AppraisalEvaluationList: NULL/blank segment
/// defaults to "IBG", matched case-insensitively. Used to snapshot the composite score
/// at completion so later config edits do not rewrite completed evaluations.
/// </summary>
internal static class EvaluationWeights
{
    public static async Task<Dictionary<int, decimal>> LoadAsync(
        AppraisalDbContext db,
        Guid appraisalId,
        CancellationToken cancellationToken)
    {
        var segment = await db.Appraisals
            .Where(a => a.Id == appraisalId)
            .Select(a => a.BankingSegment)
            .FirstOrDefaultAsync(cancellationToken);

        // Mirror the view EXACTLY: ISNULL(BankingSegment, 'IBG') maps only NULL to IBG.
        // A blank/empty-string segment must stay blank so it matches no config (score 0),
        // otherwise the C# snapshot would disagree with the view's live computation.
        var seg = (segment ?? "IBG").ToLower();

        return await db.EvaluationCriteriaConfigs
            .AsNoTracking()
            .Where(c => c.BankingSegment.ToLower() == seg)
            .ToDictionaryAsync(c => c.CriteriaSlot, c => c.Weight, cancellationToken);
    }
}
