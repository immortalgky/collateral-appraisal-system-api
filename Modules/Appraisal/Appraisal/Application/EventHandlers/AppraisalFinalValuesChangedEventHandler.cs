using Appraisal.Application.Services;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// Recomputes the appraisal-level ValuationAnalyses summary whenever a PricingAnalysis
/// FinalAppraisedValue changes, by delegating to <see cref="AppraisalValuationSummaryService"/>.
///
/// Runs inside DispatchDomainEventInterceptor.SavingChangesAsync (PRE-save). That is valid ONLY
/// because the analysis whose value just changed is a Modified entity — its row already exists in
/// the database, so the service's SQL sum returns it (identity resolution then hands back the
/// tracked instance carrying the in-memory value).
///
/// A tracking query is BLIND to Added entities — rows not yet inserted — and still returns Deleted
/// ones. So flows that INSERT new analyses (CI copy) or DELETE existing ones (group delete) must NOT
/// rely on this pre-save event; they call AppraisalValuationSummaryService.RecomputeAsync directly
/// POST-save instead. Emitting the event on Added clones is exactly what wrote AppraisedValue = 0.
/// </summary>
public class AppraisalFinalValuesChangedEventHandler(
    AppraisalDbContext db,
    IAppraisalRepository appraisalRepository,
    AppraisalValuationSummaryService summaryService,
    ILogger<AppraisalFinalValuesChangedEventHandler> logger
) : INotificationHandler<AppraisalFinalValuesChangedEvent>
{
    public async Task Handle(AppraisalFinalValuesChangedEvent notification, CancellationToken ct)
    {
        // PropertyGroup is an owned entity — can only be reached via the Appraisal aggregate.
        var appraisal = db.Appraisals.Local.FirstOrDefault(a => a.Groups.Any(g => g.Id == notification.PropertyGroupId));

        if (appraisal is not null)
        {
            var properties = db.Entry(appraisal).Collection(a => a.Properties);

            // Added = not in the database yet, so every property it has was added through this context:
            // the tracked collection is already complete (and a repo query would find nothing).
            if (!properties.IsLoaded && db.Entry(appraisal).State == EntityState.Added)
                properties.IsLoaded = true;

            if (!properties.IsLoaded)
            {
                // Tracked without its properties — fill them in on the tracked instance (split query).
                // `?? appraisal`: the load applies the soft-delete filter and may return null; keep the
                // tracked instance so RecomputeAsync's guard fails loudly instead of a silent skip.
                appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(appraisal.Id, ct) ?? appraisal;
            }
        }
        else
        {
            var appraisalId = await db.Appraisals
                .Where(a => a.Groups.Any(g => g.Id == notification.PropertyGroupId))
                .Select(a => (Guid?)a.Id)
                .FirstOrDefaultAsync(ct);

            appraisal = appraisalId is null
                ? null
                : await appraisalRepository.GetByIdWithPropertiesAsync(appraisalId.Value, ct);
        }

        if (appraisal is null)
        {
            logger.LogWarning(
                "AppraisalFinalValuesChangedEvent: PropertyGroup {PropertyGroupId} not found — skipping.",
                notification.PropertyGroupId);
            return;
        }

        // Pass the aggregate we just resolved so RecomputeAsync doesn't look it up again, and
        // isBlock: false — AppraisalFinalValuesChangedEvent only fires for PropertyGroup analyses,
        // which never belong to a block/project appraisal, so the block-detection query is skippable.
        await summaryService.RecomputeAsync(appraisal.Id, ct, appraisal: appraisal, isBlock: false);
    }
}
