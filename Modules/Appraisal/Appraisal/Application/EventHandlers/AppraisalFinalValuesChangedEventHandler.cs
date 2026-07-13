using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Time;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// Persists the appraisal-level summary (TotalAppraisalPrice, ForceSellingPrice, BuildingInsurance)
/// into ValuationAnalyses whenever a PricingAnalysis FinalAppraisedValue changes.
///
/// Runs inside DispatchDomainEventInterceptor.SavingChangesAsync (pre-save). To see the just-modified
/// in-memory values, all reads materialize tracked entities and aggregate client-side — SQL aggregate
/// functions like SumAsync bypass the ChangeTracker and would return stale DB values.
///
/// Do NOT call SaveChangesAsync — tracked mutations are flushed by the outer save. The outbox message
/// staged here is drained into the same SaveChangesAsync by DispatchDomainEventInterceptor (which
/// dispatches domain events, then drains the outbox), so publish is atomic with the summary upsert.
/// </summary>
public class AppraisalFinalValuesChangedEventHandler(
    AppraisalDbContext db,
    IIntegrationEventOutbox outbox,
    IDateTimeProvider dateTimeProvider,
    ILogger<AppraisalFinalValuesChangedEventHandler> logger
) : INotificationHandler<AppraisalFinalValuesChangedEvent>
{
    public async Task Handle(AppraisalFinalValuesChangedEvent notification, CancellationToken ct)
    {
        // PropertyGroup is an owned entity — can only be reached via the Appraisal aggregate.
        var appraisal = await db.Appraisals
            .FirstOrDefaultAsync(a => a.Groups.Any(g => g.Id == notification.PropertyGroupId), ct);

        if (appraisal is null)
        {
            logger.LogWarning(
                "AppraisalFinalValuesChangedEvent: PropertyGroup {PropertyGroupId} not found — skipping.",
                notification.PropertyGroupId);
            return;
        }

        var appraisalId = appraisal.Id;
        var propertyGroupIds = appraisal.Groups.Select(g => g.Id).ToList();

        var pricingAnalyses = await db.PricingAnalyses
            .Where(pa => pa.SubjectType == PricingAnalysisSubjectType.PropertyGroup
                         && pa.AnchorId.HasValue
                         && propertyGroupIds.Contains(pa.AnchorId!.Value))
            .ToListAsync(ct);

        var total = pricingAnalyses.Sum(pa => pa.FinalAppraisedValue ?? 0m);
        var forced = total * 0.70m;

        var pricingAnalysisIds = pricingAnalyses.Select(pa => pa.Id).ToList();
        var distinctApproaches = await db.PricingAnalysisApproaches
            .Where(a => pricingAnalysisIds.Contains(a.PricingAnalysisId))
            .ToListAsync(ct);

        var selectedApproachTypes = distinctApproaches
            .Where(a => a.IsSelected)
            .Select(a => a.ApproachType)
            .Distinct()
            .ToList();

        var approach = selectedApproachTypes.Count == 1 ? selectedApproachTypes[0] : "Combined";

        // BuildingAppraisalDetail is owned by AppraisalProperty (OwnsOne) — reach via the nav.
        var properties = await db.AppraisalProperties
            .Where(ap => ap.AppraisalId == appraisalId)
            .ToListAsync(ct);

        var insuranceTotal = properties
            .Where(ap => ap.BuildingDetail != null)
            .SelectMany(ap => ap.BuildingDetail!.DepreciationDetails)
            .Where(d => d.IsBuilding)
            .Sum(d => d.PriceAfterDepreciation);

        var assignmentIds = await db.AppraisalAssignments
            .Where(a => a.AppraisalId == appraisalId)
            .Select(a => a.Id)
            .ToListAsync(ct);

        DateTime date;
        if (assignmentIds.Count > 0)
        {
            var appointments = await db.Appointments
                .Where(ap => assignmentIds.Contains(ap.AssignmentId))
                .ToListAsync(ct);

            date = appointments.Count > 0
                ? appointments.Max(ap => ap.AppointmentDateTime)
                : DateTime.Now;
        }
        else
        {
            date = DateTime.Now;
        }

        var row = db.ValuationAnalyses.Local.FirstOrDefault(v => v.AppraisalId == appraisalId)
                  ?? await db.ValuationAnalyses.FirstOrDefaultAsync(v => v.AppraisalId == appraisalId, ct);

        if (row is null)
        {
            row = ValuationAnalysis.Create(appraisalId, approach, date);
            db.ValuationAnalyses.Add(row);
        }

        row.UpdateSummary(approach, date, total, forced, insuranceTotal);

        // Surface the new appraisal-level appraised value to the Workflow module so the
        // approval-tier switch / committee selection route on appraised value (not facility limit).
        // CorrelationId = RequestId is the workflow instance's correlation key.
        outbox.Publish(new AppraisalValueChangedIntegrationEvent
        {
            AppraisalId = appraisalId,
            CorrelationId = appraisal.RequestId,
            AppraisedValue = total,
            OccurredOn = dateTimeProvider.ApplicationNow
        });

        logger.LogDebug(
            "ValuationAnalyses upserted for AppraisalId: {AppraisalId} — Total: {Total}, Forced: {Forced}, Insurance: {Insurance}",
            appraisalId, total, forced, insuranceTotal);
    }
}
