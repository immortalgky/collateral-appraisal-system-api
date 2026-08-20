using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Time;

namespace Appraisal.Application.Services;

/// <summary>
/// Recomputes the appraisal-level ValuationAnalyses summary (AppraisedValue / ForcedSaleValue /
/// InsuranceValue / ValuationApproach), upserts the row, and stages
/// <see cref="AppraisalValueChangedIntegrationEvent"/> on the outbox. The total comes from the
/// PropertyGroup PricingAnalyses for a normal appraisal, or from the unsold ProjectUnitPrices for a
/// block/project appraisal (detected by a Project row) — see the branch in RecomputeAsync.
/// <para>
/// Never calls SaveChangesAsync — the tracked mutations and the outbox message are flushed by the
/// caller's save, which is what keeps the upsert and the publish atomic.
/// </para>
/// <para>
/// CALLERS MUST ENSURE the PricingAnalyses they care about are already IN THE DATABASE. This reads
/// them with SQL: a tracking query resolves identity for Modified entities but is blind to Added
/// ones, which have no row yet, and still returns Deleted ones until the save runs. Invoked
/// pre-save from <see cref="EventHandlers.AppraisalFinalValuesChangedEventHandler"/> that is fine
/// (the changed analysis is Modified); flows that INSERT new analyses or DELETE existing ones must
/// call this POST-save (see AppraisalCreationService / DeletePropertyGroupCommandHandler).
/// </para>
/// </summary>
public class AppraisalValuationSummaryService(
    AppraisalDbContext db,
    IIntegrationEventOutbox outbox,
    IDateTimeProvider dateTimeProvider,
    ForceSaleRateResolver forceSaleRateResolver,
    ILogger<AppraisalValuationSummaryService> logger)
{
    /// <param name="valuationDate">
    /// When provided, used verbatim as the ValuationDate. Callers running BEFORE the appointment row
    /// exists (CI copy recomputes at Phase 2, the appointment is added at Phase 3) must pass the
    /// known appointment date here — otherwise the appointment-derived fallback below sees no rows and
    /// stamps ApplicationNow. When null, the date is derived from the appraisal's appointments as usual.
    /// </param>
    /// <param name="appraisal">
    /// The already-resolved aggregate, when the caller has one in hand (e.g. the pre-save event
    /// handler resolves it by group). When null it is looked up by <paramref name="appraisalId"/>.
    /// Must be the aggregate for <paramref name="appraisalId"/>.
    /// </param>
    /// <param name="isBlock">
    /// Optional hint: pass <c>false</c> from callers that already know the appraisal is a normal
    /// PropertyGroup appraisal (e.g. the pre-save AppraisalFinalValuesChangedEvent handler, whose
    /// event only fires for PropertyGroup analyses) to skip the block-detection query on the hot
    /// pricing-save path. When null the block flag is resolved with a query.
    /// </param>
    public async Task RecomputeAsync(
        Guid appraisalId,
        CancellationToken ct,
        DateTime? valuationDate = null,
        Domain.Appraisals.Appraisal? appraisal = null,
        bool? isBlock = null)
    {
        appraisal ??= db.Appraisals.Local.FirstOrDefault(a => a.Id == appraisalId)
                      ?? await db.Appraisals.FirstOrDefaultAsync(a => a.Id == appraisalId, ct);

        if (appraisal is null)
        {
            logger.LogWarning(
                "AppraisalValuationSummaryService: Appraisal {AppraisalId} not found — skipping.",
                appraisalId);
            return;
        }

        decimal total;
        decimal insuranceTotal;
        string approach;

        // A block/project appraisal prices per ProjectModel (SubjectType = ProjectModel), fanned out
        // to per-unit ProjectUnitPrices — it has no PropertyGroup PAs. Detect it by the presence of a
        // Project row; the two paths never mix (non-block appraisals have no Project). Callers on the
        // hot PropertyGroup pricing-save path pass isBlock=false to skip this query.
        var block = isBlock ?? await db.Projects.AnyAsync(p => p.AppraisalId == appraisalId, ct);

        if (block)
        {
            // Unsold-unit rollup — mirrors GetDecisionSummary's block totals (KEEP IN SYNC with
            // Features/DecisionSummary/GetDecisionSummary/GetDecisionSummaryQueryHandler.cs block SQL).
            // Read via THIS DbContext so ProjectUnitPrices just saved by CalculateProjectUnitPrices are
            // visible inside its transaction; a separate Dapper connection would not see them.
            var unitPrices = await (
                from pu in db.ProjectUnits
                join pup in db.ProjectUnitPrices on pu.Id equals pup.ProjectUnitId
                join pm in db.ProjectModels on pu.ProjectModelId equals pm.Id
                join p in db.Projects on pm.ProjectId equals p.Id
                where p.AppraisalId == appraisalId && !pu.IsSold
                select new { pup.TotalAppraisalValueRounded, pup.CoverageAmount })
                .ToListAsync(ct);

            total = unitPrices.Sum(x => x.TotalAppraisalValueRounded ?? 0m);
            insuranceTotal = unitPrices.Sum(x => x.CoverageAmount ?? 0m);

            // Approach label from the ProjectModel PAs' selected approaches (single distinct → that,
            // else "Combined"), matching the PropertyGroup path's rule.
            var modelApproachTypes = await (
                from pa in db.PricingAnalyses
                join pm in db.ProjectModels on pa.AnchorId equals pm.Id
                join p in db.Projects on pm.ProjectId equals p.Id
                join paa in db.PricingAnalysisApproaches on pa.Id equals paa.PricingAnalysisId
                where p.AppraisalId == appraisalId
                      && pa.SubjectType == PricingAnalysisSubjectType.ProjectModel
                      && paa.IsSelected
                select paa.ApproachType).Distinct().ToListAsync(ct);

            approach = modelApproachTypes.Count == 1 ? modelApproachTypes[0] : "Combined";
        }
        else
        {
            var propertyGroupIds = appraisal.Groups.Select(g => g.Id).ToList();

            var pricingAnalyses = await db.PricingAnalyses
                .Where(pa => pa.SubjectType == PricingAnalysisSubjectType.PropertyGroup
                             && pa.AnchorId.HasValue
                             && propertyGroupIds.Contains(pa.AnchorId!.Value))
                .ToListAsync(ct);

            total = pricingAnalyses.Sum(pa => pa.FinalAppraisedValue ?? 0m);

            var pricingAnalysisIds = pricingAnalyses.Select(pa => pa.Id).ToList();
            var distinctApproaches = await db.PricingAnalysisApproaches
                .Where(a => pricingAnalysisIds.Contains(a.PricingAnalysisId))
                .ToListAsync(ct);

            var selectedApproachTypes = distinctApproaches
                .Where(a => a.IsSelected)
                .Select(a => a.ApproachType)
                .Distinct()
                .ToList();

            approach = selectedApproachTypes.Count == 1 ? selectedApproachTypes[0] : "Combined";

            // BuildingAppraisalDetail is owned by AppraisalProperty (OwnsOne) — reach via the nav.
            var properties = await db.AppraisalProperties
                .Where(ap => ap.AppraisalId == appraisalId)
                .ToListAsync(ct);

            // Insurance is the sum of every insurable structure on the appraisal. The two property
            // families derive their figure differently but land in the same column:
            //   buildings — depreciated structure value (see claude/tasks/fix-building-insurance-source.md)
            //   condos    — rate-derived coverage amount, RatePerSqm × UsableArea
            //               (Features/Appraisals/CondoFireInsuranceCalculator.cs)
            // Land is deliberately excluded, matching the "buildings only — excludes land" UI hint.
            //
            // KEEP IN SYNC with Features/DecisionSummary/BuildingInsuranceCalculator.cs, which computes
            // the same total in SQL for the read/save path.
            var buildingInsurance = properties
                .Where(ap => ap.BuildingDetail != null)
                .SelectMany(ap => ap.BuildingDetail!.DepreciationDetails)
                .Where(d => d.IsBuilding)
                .Sum(d => d.PriceAfterDepreciation);

            // Covers lease-agreement condo too — it populates this same CondoDetail nav.
            var condoInsurance = properties
                .Where(ap => ap.CondoDetail != null)
                .Sum(ap => ap.CondoDetail!.BuildingInsurancePrice ?? 0m);

            insuranceTotal = buildingInsurance + condoInsurance;
        }

        var assignmentIds = await db.AppraisalAssignments
            .Where(a => a.AppraisalId == appraisalId)
            .Select(a => a.Id)
            .ToListAsync(ct);

        var row = db.ValuationAnalyses.Local.FirstOrDefault(v => v.AppraisalId == appraisalId)
                  ?? await db.ValuationAnalyses.FirstOrDefaultAsync(v => v.AppraisalId == appraisalId, ct);

        // Off-system external engagement (AssignmentMethod = "Offline"): the external company was
        // engaged outside CAS and never booked an appointment, so the appointment-derived branch
        // below would fall through to ApplicationNow and overwrite the date an internal appraiser
        // keyed off the company's paper book — on EVERY pricing save. Preserve what is stored.
        var preserveKeyedDate = row is not null
                                && !valuationDate.HasValue
                                && await IsOfflineExternalAsync(appraisalId, ct);

        DateTime date;
        if (valuationDate.HasValue)
        {
            // Explicit date from a caller that recomputes before the appointment row is persisted.
            date = valuationDate.Value;
        }
        else if (preserveKeyedDate)
        {
            date = row!.ValuationDate;
        }
        else
        {
            // Cancelled appointments are excluded: every read-side surface that shows the appraisal
            // date now reads this column, and they all filter Status <> 'Cancelled'. Including them
            // here would let a cancelled (often later-dated) slot become the displayed valuation date.
            // Status is a plain string on Appointment, so this translates server-side.
            var appointments = assignmentIds.Count > 0
                ? await db.Appointments
                    .Where(ap => assignmentIds.Contains(ap.AssignmentId)
                                 && ap.Status != "Cancelled")
                    .ToListAsync(ct)
                : [];

            // No appointment-derived date available — either none was ever booked, or every one of
            // them has been cancelled. Fall back to what is ALREADY STORED before inventing a date.
            //
            // ApplicationNow here is a moving target: this method re-runs on every pricing edit,
            // property delete, unit-price calculation and final-values change, so an appraisal whose
            // only appointment was cancelled would have its appraisal date silently dragged forward
            // to "today" on each save. Since ValuationDate now LEADS every read surface — the printed
            // book, both AS400 result feeds, the 360 view, decision summary, History Search — and
            // anchors the +5-year reappraisal clock in vw_ReappraisalCandidates / RCAS002, that
            // rewrite would propagate a wrong appraisal date to the bank's reappraisal schedule.
            // Preserving keeps the last real date (usually the appointment that was later cancelled).
            //
            // ApplicationNow survives only for a genuinely new row with nothing to preserve;
            // vw_AppraisalValidationContext.ExternalAppraisalDateRecorded is what flags those.
            date = appointments.Count > 0
                ? appointments.Max(ap => ap.AppointmentDateTime)
                : row?.ValuationDate ?? dateTimeProvider.ApplicationNow;
        }

        // Capture the prior appraised value so the integration event below is published only when it
        // actually changes (null = no prior row → always publish). The row itself is still upserted
        // unconditionally, so insurance / approach columns stay fresh even on an unchanged total.
        var previousAppraisedValue = row?.AppraisedValue;

        if (row is null)
        {
            row = ValuationAnalysis.Create(appraisalId, approach, date);
            db.ValuationAnalyses.Add(row);
        }

        // Force-sale rate resolution via the shared resolver (override -> block project assumption
        // -> system default -> 70m). The project-assumption lookup is a plain Dapper read via
        // ISqlConnectionFactory, not a tracked-entity query, so it's safe to call here.
        var rate = await forceSaleRateResolver.ResolveAsync(appraisalId, row.ForceSaleRate, ct);
        var forced = total * rate / 100m;

        row.UpdateSummary(
            approach,
            date,
            total,
            Math.Round(forced / 1000, MidpointRounding.AwayFromZero) * 1000,
            Math.Round(insuranceTotal / 1000, MidpointRounding.AwayFromZero) * 1000);

        // Surface the new appraisal-level appraised value to the Workflow module so the
        // approval-tier switch / committee selection route on appraised value (not facility limit).
        // CorrelationId = RequestId is the workflow instance's correlation key. Publish ONLY when the
        // appraised value actually changed — the approval tier keys off this value, so republishing an
        // unchanged total (e.g. an insurance-only refresh, or a re-run of CalculateProjectUnitPrices
        // with no price change) is redundant cross-module churn. Mirrors the domain rollup's gate.
        if (previousAppraisedValue != total)
        {
            outbox.Publish(new AppraisalValueChangedIntegrationEvent
            {
                AppraisalId = appraisalId,
                CorrelationId = appraisal.RequestId,
                AppraisedValue = total,
                OccurredOn = dateTimeProvider.ApplicationNow
            });
        }

        logger.LogDebug(
            "ValuationAnalyses upserted for AppraisalId: {AppraisalId} — Total: {Total}, Forced: {Forced}, Insurance: {Insurance}",
            appraisalId, total, forced, insuranceTotal);
    }

    /// <summary>
    /// Points ValuationDate at a newly booked or rescheduled appointment, WITHOUT recomputing the
    /// monetary summary.
    ///
    /// <para>
    /// ValuationDate is only otherwise written by <see cref="RecomputeAsync"/>, and every caller of
    /// that is a pricing or property change — no appointment command triggers it. So an appointment
    /// booked for 1 Mar, worked and priced (ValuationDate = 1 Mar), then rescheduled to 15 Mar with
    /// no pricing save afterwards, left every appraisal-date surface reporting 1 Mar indefinitely:
    /// the 360 view, decision summary, the printed book, both AS400 result APIs, History Search, and
    /// the +5-year reappraisal anchor in vw_ReappraisalCandidates / vw_RCAS002_ReappraisalDue. The
    /// two dates never reconverged unless someone happened to re-save pricing.
    /// </para>
    ///
    /// <para>
    /// The date is passed in rather than re-derived: the appointment is Added or Modified but not yet
    /// flushed when the appointment handlers call this, and <see cref="AppraisalDateResolver"/>
    /// projects to a scalar — a projection bypasses identity resolution and would read the stale
    /// database value. Callers already hold the authoritative new date.
    /// </para>
    ///
    /// <para>
    /// No-ops when the row does not exist yet (the next recompute creates it with the right date) or
    /// when the engagement is off-system, where the keyed book date outranks any appointment —
    /// the same rule <see cref="RecomputeAsync"/> applies.
    /// </para>
    /// </summary>
    public async Task SyncValuationDateFromAppointmentAsync(
        Guid appraisalId,
        DateTime appointmentDate,
        CancellationToken ct)
    {
        var row = db.ValuationAnalyses.Local.FirstOrDefault(v => v.AppraisalId == appraisalId)
                  ?? await db.ValuationAnalyses.FirstOrDefaultAsync(v => v.AppraisalId == appraisalId, ct);

        if (row is null)
            return;

        if (await IsOfflineExternalAsync(appraisalId, ct))
            return;

        row.SetValuationDate(appointmentDate);

        logger.LogDebug(
            "ValuationDate synced from appointment for AppraisalId: {AppraisalId} — {Date}",
            appraisalId, appointmentDate);
    }

    /// <summary>
    /// True when the appraisal's active assignment records an engagement the bank arranged outside
    /// the system (written by SetOfflineExternalEngagementCommandHandler). Uses the same
    /// active-assignment rule as CompanyAssignedIntegrationEventHandler — latest row that is
    /// neither Rejected nor Cancelled — so all three paths agree on which assignment is current.
    /// </summary>
    private async Task<bool> IsOfflineExternalAsync(Guid appraisalId, CancellationToken ct)
    {
        // Filter by status AFTER materialization: AssignmentStatus is a HasConversion value object
        // (AppraisalAssignmentConfiguration.cs) and EF cannot decompose it in SQL — a server-side
        // Where on AssignmentStatus.Code throws "could not be translated". Same reasoning and same
        // shape as GetAssignmentsQueryHandler. Row count per appraisal is tiny.
        var rows = await db.AppraisalAssignments
            .AsNoTracking()
            .Where(a => a.AppraisalId == appraisalId)
            .Select(a => new { a.AssignmentStatus, a.AssignmentMethod, a.AssignedAt, a.CreatedAt, a.Id })
            .ToListAsync(ct);

        var method = rows
            .Where(a => a.AssignmentStatus != AssignmentStatus.Rejected
                        && a.AssignmentStatus != AssignmentStatus.Cancelled)
            .OrderByDescending(a => a.AssignedAt)
            .ThenByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => a.AssignmentMethod)
            .FirstOrDefault();

        return string.Equals(
            method,
            AppraisalAssignment.OfflineAssignmentMethod,
            StringComparison.OrdinalIgnoreCase);
    }
}
