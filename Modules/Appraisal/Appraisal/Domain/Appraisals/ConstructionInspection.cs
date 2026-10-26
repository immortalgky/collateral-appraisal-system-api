namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Construction inspection record for an appraisal property (1:1).
/// Supports two modes: Full Detail (grouped work items) or Summary (simple overview).
/// Owned by AppraisalProperty via OwnsOne (stored in separate table).
/// </summary>
public class ConstructionInspection : Entity<Guid>
{
    public Guid AppraisalPropertyId { get; private set; }
    public bool IsFullDetail { get; private set; }
    public decimal TotalValue { get; private set; }

    // Summary mode only
    public string? SummaryDetail { get; private set; }

    // The four Summary* figures below are the inspection-level result in BOTH modes, so every reader
    // takes progress and value from them without branching on IsFullDetail. Despite the name they are
    // not summary-mode-only any more.
    //   Summary mode:     the two percentages are keyed by the inspector; the two values are computed
    //                     here as TotalValue x pct / 100 (a value sent by the client is ignored).
    //   Full-detail mode: all four are computed here from the work rows and overwritten on every save.
    //                     A work row's contribution is ProportionPct x ProgressPct / 100.
    // The one value the inspector may set is the current one (currentValueOverride on the write methods):
    // a percentage keyed at 2 dp can be a few baht off the figure worked out from the unrounded one.
    // The override is stored in SummaryCurrentValue itself; IsCurrentValueOverridden tells it apart.

    /// <summary>Previous round's progress of this inspection, 0-100. Keyed in summary mode, computed in full-detail mode.</summary>
    public decimal? SummaryPreviousProgressPct { get; private set; }

    /// <summary>Previous round's value in whole baht. Computed here, or carried from an overridden current value of the last round; never taken from the client.</summary>
    public decimal? SummaryPreviousValue { get; private set; }

    /// <summary>Current round's progress of this inspection, 0-100. Keyed in summary mode, computed in full-detail mode.</summary>
    public decimal? SummaryCurrentProgressPct { get; private set; }

    /// <summary>
    /// Current round's value in whole baht. Computed here, or the inspector's override when one was given;
    /// the SummaryCurrentValue the client sends is never taken.
    /// </summary>
    public decimal? SummaryCurrentValue { get; private set; }

    /// <summary>
    /// True when <see cref="SummaryCurrentValue"/> is not what the stored base and progress yield. Judged against the
    /// STORED inputs, not today's building cost, so an inspection whose base later moved is not reported as overridden.
    /// A difference under one baht is rounding (rows saved before CA-614 can carry satang), not an override.
    /// </summary>
    public bool IsCurrentValueOverridden =>
        SummaryCurrentValue is { } stored && ComputedCurrentValue() is { } computed && DiffersByABaht(stored, computed);

    /// <summary>
    /// Free-text note printed as the remark row of the construction summary report.
    /// Mode-independent: captured in both Summary and Full Detail mode, and preserved
    /// across a mode switch.
    /// </summary>
    public string? Remark { get; private set; }

    // Document reference (summary mode)
    public Guid? DocumentId { get; private set; }
    public string? FileName { get; private set; }
    public string? FilePath { get; private set; }
    public string? FileExtension { get; private set; }
    public string? MimeType { get; private set; }
    public long? FileSizeBytes { get; private set; }

    // Full Detail Mode - work details
    private readonly List<ConstructionWorkDetail> _workDetails = [];
    public IReadOnlyList<ConstructionWorkDetail> WorkDetails => _workDetails.AsReadOnly();

    private ConstructionInspection()
    {
    }

    // Not persisted: the override given to the write method in progress, so the RecomputeSummary calls that
    // follow it (ClearWorkDetails, ComputeAllValues) keep it instead of wiping it.
    private decimal? _currentValueOverride;

    // Not persisted: the previous-round figures as they were before the write in progress began. See RecomputeSummary.
    private (decimal? Pct, decimal? Value, decimal? Computed, decimal Total)? _previousBaseline;

    /// <summary>Storage limit for <see cref="Remark"/>; mirrored by the EF configuration.</summary>
    public const int RemarkMaxLength = 4000;

    /// <summary>Storage limit for <see cref="SummaryDetail"/>; mirrored by the EF configuration.</summary>
    public const int SummaryDetailMaxLength = 1000;

    // Length is enforced here rather than in a request validator because five of the twelve
    // commands that carry ConstructionInspectionData have no validator at all — the entity is the
    // only chokepoint every write path passes through. DomainException surfaces as HTTP 400, so an
    // over-long value is reported as bad input instead of failing at SaveChanges as a 500.
    private static string? EnsureFits(string? value, int maxLength, string fieldName)
    {
        if (value is not null && value.Length > maxLength)
            throw new DomainException($"{fieldName} must be at most {maxLength} characters.");

        return value;
    }

    /// <summary>
    /// Create a full-detail construction inspection.
    /// </summary>
    public static ConstructionInspection CreateFullDetail(
        Guid appraisalPropertyId,
        decimal totalValue,
        string? remark,
        decimal? currentValueOverride = null)
    {
        var inspection = new ConstructionInspection
        {
            AppraisalPropertyId = appraisalPropertyId,
            IsFullDetail = true,
            TotalValue = ConstructionMoney.ToBaht(totalValue),
            Remark = EnsureFits(remark, RemarkMaxLength, nameof(Remark))
        };
        inspection._currentValueOverride = inspection.EnsureOverrideInRange(currentValueOverride);
        inspection.RecomputeSummary();
        inspection._previousBaseline = null;
        return inspection;
    }

    /// <summary>
    /// Create a summary-only construction inspection.
    /// </summary>
    public static ConstructionInspection CreateSummary(
        Guid appraisalPropertyId,
        decimal totalValue,
        string? summaryDetail,
        decimal? summaryPreviousProgressPct,
        decimal? summaryCurrentProgressPct,
        string? remark,
        decimal? currentValueOverride = null)
    {
        var inspection = new ConstructionInspection
        {
            AppraisalPropertyId = appraisalPropertyId,
            IsFullDetail = false,
            TotalValue = ConstructionMoney.ToBaht(totalValue),
            SummaryDetail = EnsureFits(summaryDetail, SummaryDetailMaxLength, nameof(SummaryDetail)),
            SummaryPreviousProgressPct = ToColumnPct(summaryPreviousProgressPct),
            SummaryCurrentProgressPct = ToColumnPct(summaryCurrentProgressPct),
            Remark = EnsureFits(remark, RemarkMaxLength, nameof(Remark))
        };
        inspection._currentValueOverride = inspection.EnsureOverrideInRange(currentValueOverride);
        inspection.RecomputeSummary();
        inspection._previousBaseline = null;
        return inspection;
    }

    /// <summary>
    /// Switch to summary mode. Clears all work details.
    /// </summary>
    public void UpdateSummary(
        decimal totalValue,
        string? summaryDetail,
        decimal? summaryPreviousProgressPct,
        decimal? summaryCurrentProgressPct,
        string? remark,
        decimal? currentValueOverride = null)
    {
        _previousBaseline = PreviousSnapshot();

        // Clear work details when switching to summary mode
        if (IsFullDetail)
            _workDetails.Clear();

        IsFullDetail = false;
        TotalValue = ConstructionMoney.ToBaht(totalValue);
        SummaryDetail = EnsureFits(summaryDetail, SummaryDetailMaxLength, nameof(SummaryDetail));
        SummaryPreviousProgressPct = ToColumnPct(summaryPreviousProgressPct);
        SummaryCurrentProgressPct = ToColumnPct(summaryCurrentProgressPct);
        Remark = EnsureFits(remark, RemarkMaxLength, nameof(Remark));
        _currentValueOverride = EnsureOverrideInRange(currentValueOverride);
        RecomputeSummary();
        _previousBaseline = null;   // no ComputeAllValues follows a summary write
    }

    /// <summary>
    /// Switch to full detail mode. Clears the summary-only fields, but no longer clears
    /// <see cref="Remark"/> as part of that: the remark is captured in both modes, so a mode
    /// switch on its own must not wipe it.
    ///
    /// The remark is still a whole-value overwrite from the caller, exactly like every other
    /// field on ConstructionInspectionData — the property endpoints replace the inspection from
    /// the submitted form rather than patching it, so passing null means "store no remark", not
    /// "leave the stored one alone".
    /// </summary>
    public void UpdateFullDetail(decimal totalValue, string? remark, decimal? currentValueOverride = null)
    {
        _previousBaseline = PreviousSnapshot();

        // Clear summary-only fields when switching to full detail mode. The four figures are
        // recomputed from the work rows below, so the keyed percentages do not survive the switch.
        if (!IsFullDetail)
        {
            SummaryDetail = null;
            ClearDocument();
        }

        IsFullDetail = true;
        TotalValue = ConstructionMoney.ToBaht(totalValue);
        Remark = EnsureFits(remark, RemarkMaxLength, nameof(Remark));
        _currentValueOverride = EnsureOverrideInRange(currentValueOverride);
        RecomputeSummary();
    }

    public void SetDocument(
        Guid documentId,
        string? fileName,
        string? filePath,
        string? fileExtension = null,
        string? mimeType = null,
        long? fileSizeBytes = null)
    {
        DocumentId = documentId;
        FileName = fileName;
        FilePath = filePath;
        FileExtension = fileExtension;
        MimeType = mimeType;
        FileSizeBytes = fileSizeBytes;
    }

    public void ClearDocument()
    {
        DocumentId = null;
        FileName = null;
        FilePath = null;
        FileExtension = null;
        MimeType = null;
        FileSizeBytes = null;
    }

    /// <summary>
    /// Add a work detail item (full detail mode).
    /// </summary>
    public ConstructionWorkDetail AddWorkDetail(
        Guid constructionWorkGroupId,
        string workItemName,
        int displayOrder,
        decimal proportionPct,
        decimal previousProgressPct,
        decimal currentProgressPct,
        Guid? constructionWorkItemId = null)
    {
        var detail = ConstructionWorkDetail.Create(
            Id,
            constructionWorkGroupId,
            workItemName,
            displayOrder,
            proportionPct,
            previousProgressPct,
            currentProgressPct,
            constructionWorkItemId);

        _workDetails.Add(detail);
        return detail;
    }

    public void ClearWorkDetails()
    {
        RememberPrevious();
        _workDetails.Clear();
        RecomputeSummary();
    }

    /// <summary>
    /// Compute all derived values for work details, then the inspection-level summary figures.
    /// </summary>
    public void ComputeAllValues()
    {
        RememberPrevious();
        foreach (var detail in _workDetails)
        {
            detail.ComputeValues(TotalValue);
        }

        RecomputeSummary();
        _previousBaseline = null;   // the write is over; a later recompute must snapshot afresh
    }

    /// <summary>
    /// Brings the four Summary* figures in line with the current mode (see the property remarks).
    /// Summary mode keeps the keyed percentages and derives the money; full-detail mode derives all
    /// four from the work rows. The current value is the inspector's override when there is one.
    /// </summary>
    private void RecomputeSummary()
    {
        RememberPrevious();

        if (IsFullDetail)
        {
            // The percentage columns are decimal(7,4). Current rounds each row first because the stored
            // CurrentProportionPct already is rounded that way and the readers this replaced summed the
            // stored rows; previous had no stored row figure, so it is summed whole and rounded once.
            SummaryCurrentProgressPct = _workDetails.Sum(d =>
                Math.Round(d.CurrentProportionPct, 4, MidpointRounding.AwayFromZero));
            SummaryPreviousProgressPct = Math.Round(
                _workDetails.Sum(d => d.ProportionPct * d.PreviousProgressPct / 100m),
                4, MidpointRounding.AwayFromZero);
        }

        // The previous percentage is read-only on the screen, so when it and the base are both unchanged the
        // stored previous value is kept if it is not what the stored inputs yield — i.e. it was carried from
        // the last round's current value (CopyForNextInspection). Otherwise it is recomputed, so a new base
        // or a mode switch never leaves it stale.
        var before = _previousBaseline!.Value;
        var carried = before.Value is { } kept && before.Computed is { } was && DiffersByABaht(kept, was)
                      && before.Pct == SummaryPreviousProgressPct && before.Total == TotalValue;
        SummaryPreviousValue = carried ? before.Value : ComputedPreviousValue();

        // The override replaces a computed value, so it needs one: summary mode with a keyed percentage, or
        // full-detail with at least one work row. With nothing keyed there is nothing to override.
        var computedCurrent = ComputedCurrentValue();
        var hasProgress = computedCurrent is not null && (!IsFullDetail || _workDetails.Count > 0);
        SummaryCurrentValue = hasProgress && _currentValueOverride is { } v ? v : computedCurrent;
    }

    // First snapshot wins within a write: UpdateSummary/UpdateFullDetail start a fresh one, and the
    // ClearWorkDetails / ComputeAllValues calls that follow them must not replace it with a half-written state.
    private void RememberPrevious() => _previousBaseline ??= PreviousSnapshot();

    private (decimal? Pct, decimal? Value, decimal? Computed, decimal Total) PreviousSnapshot() =>
        (SummaryPreviousProgressPct, SummaryPreviousValue, ComputedPreviousValue(), TotalValue);

    private static bool DiffersByABaht(decimal a, decimal b) => Math.Abs(a - b) >= 1m;

    // The percentage columns are decimal(7,4): round what comes in so the figures computed now equal the ones
    // recomputed from the stored columns after a reload.
    private static decimal? ToColumnPct(decimal? pct) =>
        pct is null ? null : Math.Round(pct.Value, 4, MidpointRounding.AwayFromZero);

    private decimal? ComputedPreviousValue() =>
        IsFullDetail
            ? ConstructionMoney.ToBaht(_workDetails.Sum(d => d.PreviousPropertyValue))
            : ValueAt(SummaryPreviousProgressPct);

    private decimal? ComputedCurrentValue() =>
        IsFullDetail
            ? ConstructionMoney.ToBaht(_workDetails.Sum(d => d.CurrentPropertyValue))
            : ValueAt(SummaryCurrentProgressPct);

    private decimal? ValueAt(decimal? progressPct) =>
        progressPct is null ? null : ConstructionMoney.ToBaht(TotalValue * progressPct.Value / 100m);

    // Enforced here, the chokepoint every write path passes through (a DomainException surfaces as HTTP 400).
    // No own value base (TotalValue 0, the condo path) means there is nothing to override, so it is ignored.
    // Whether there is progress to override is decided later, against the final state (RecomputeSummary).
    private decimal? EnsureOverrideInRange(decimal? value)
    {
        if (value is null || TotalValue == 0)
            return null;

        var baht = ConstructionMoney.ToBaht(value.Value);
        if (baht < 0 || baht > TotalValue)
            throw new DomainException($"CurrentValueOverride must be between 0 and {TotalValue}.");

        return baht;
    }

    // The new round starts with no progress, so last round's overridden value is both its previous and its
    // current value. Only on an unchanged base — an override above a smaller base would break the bounds.
    private void CarryOverriddenValue(decimal value)
    {
        _currentValueOverride = value;
        RecomputeSummary();
        SummaryPreviousValue = value;
        _previousBaseline = null;
    }

    /// <summary>
    /// Creates a new CI shell for the next inspection round from a prior inspection.
    /// Prior's current % becomes the new previous %, and the new round's current % starts equal to it in both
    /// modes (no progress yet this round; the inspector raises it) — what the request form already shows, so a
    /// property card never reads 0% for a round that has merely not been keyed yet.
    /// </summary>
    /// <param name="totalValueOverride">
    /// The 100% base for the new round when it must not be the prior's — a condo unit pins its base to 0, so a legacy
    /// non-zero base on the prior round is not inherited. Null keeps the prior's.
    /// </param>
    public static ConstructionInspection CopyForNextInspection(
        ConstructionInspection prior,
        Guid newPropertyId,
        decimal? totalValueOverride = null)
    {
        var totalValue = totalValueOverride ?? prior.TotalValue;

        ConstructionInspection newCi;
        if (!prior.IsFullDetail)
        {
            newCi = CreateSummary(
                newPropertyId,
                totalValue: totalValue,
                summaryDetail: prior.SummaryDetail,
                summaryPreviousProgressPct: prior.SummaryCurrentProgressPct,
                summaryCurrentProgressPct: prior.SummaryCurrentProgressPct,
                remark: null);
        }
        else
        {
            newCi = CreateFullDetail(newPropertyId, totalValue, remark: null);
            foreach (var wd in prior.WorkDetails)
            {
                newCi.AddWorkDetail(
                    wd.ConstructionWorkGroupId,
                    wd.WorkItemName,
                    wd.DisplayOrder,
                    wd.ProportionPct,
                    previousProgressPct: wd.CurrentProgressPct,
                    currentProgressPct: wd.CurrentProgressPct,
                    wd.ConstructionWorkItemId);
            }

            // The copy has to derive its own values like any other write. Without this the carried-over
            // rows persist with ConstructionValue, PreviousPropertyValue and CurrentPropertyValue all
            // zero, so the appraisal book's progress table prints a column of zeros for a round whose
            // prior round was well under way. Progress percentages were never affected — they are
            // stored on the rows themselves.
            newCi.ComputeAllValues();
        }

        // Never throws: an override that no longer fits the new round's base is simply not carried.
        if (prior is { IsCurrentValueOverridden: true, SummaryCurrentValue: { } overridden }
            && newCi.TotalValue == ConstructionMoney.ToBaht(prior.TotalValue)
            && overridden <= newCi.TotalValue)
            newCi.CarryOverriddenValue(overridden);

        return newCi;
    }

    /// <summary>
    /// Overall current construction progress as a single percentage.
    /// Full-detail mode: weighted sum of (ProportionPct * CurrentProgressPct / 100) across all work details
    ///   (i.e. sum of CurrentProportionPct — each item already carries its weighted contribution).
    /// Summary mode: SummaryCurrentProgressPct, or 0 when not set.
    /// Not stored in DB — computed at runtime.
    /// </summary>
    public decimal OverallCurrentProgressPercent =>
        IsFullDetail
            ? _workDetails.Sum(d => d.CurrentProportionPct)
            : SummaryCurrentProgressPct ?? 0m;
}
