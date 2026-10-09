using Appraisal.Domain.Appraisals.Events;

namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Discriminates whether this PricingAnalysis belongs to a PropertyGroup, a ProjectModel,
/// or is a reusable market-reference analysis anchored to a non-group field.
/// </summary>
public enum PricingAnalysisSubjectType
{
    PropertyGroup = 0,
    ProjectModel = 1,
    MachineryCostRef = 2,
    IncomeLandRef = 3,
    LeaseholdLandRef = 4,
    RoomIncomeRef = 5,
    ProfitRentRef = 6
}

/// <summary>
/// Pricing analysis container.
/// <para>For <c>PropertyGroup</c> and <c>ProjectModel</c> subjects, <c>AnchorId</c> holds the respective id
/// (replacing the former <c>PropertyGroupId</c>/<c>ProjectModelId</c> columns).</para>
/// <para>For reference subjects (MachineryCostRef…ProfitRentRef), <c>AnchorId</c> is the owning
/// entity id (e.g. AppraisalProperty for machinery, IncomeAnalysisId for income/leasehold land),
/// <c>AnchorRefKey</c> is an optional discriminator within that anchor (e.g. room-type name),
/// and <c>HostMethodId</c> is the <c>PricingAnalysisMethod</c> that logically owns the field.</para>
/// <para>The composite (<c>SubjectType</c>, <c>AnchorId</c>, <c>AnchorRefKey</c>) is unique
/// (filtered index — only non-null <c>AnchorId</c> rows — allowing at most one analysis per target).</para>
/// </summary>
public class PricingAnalysis : Aggregate<Guid>
{
    private readonly List<PricingAnalysisApproach> _approaches = [];
    public IReadOnlyList<PricingAnalysisApproach> Approaches => _approaches.AsReadOnly();

    public PricingAnalysisSubjectType SubjectType { get; private set; }

    /// <summary>
    /// Generic anchor: PropertyGroup id, ProjectModel id, or reference-subject id depending on SubjectType.
    /// Always non-null (enforced by DB CHECK constraint).
    /// </summary>
    public Guid? AnchorId { get; private set; }

    /// <summary>
    /// Optional secondary discriminator within the anchor (e.g. room-type name for RoomIncomeRef).
    /// </summary>
    public string? AnchorRefKey { get; private set; }

    /// <summary>
    /// For reference rows only: the PricingAnalysisMethod whose field this reference feeds into.
    /// Used as the cleanup scope — when the host method is removed all its references are deleted.
    /// </summary>
    public Guid? HostMethodId { get; private set; }

    // Status
    public string Status { get; private set; } = null!; // Draft, InProgress, Completed

    // Final Values
    public decimal? FinalAppraisedValue { get; private set; }

    /// <summary>
    /// This analysis's own explicit calc-mode toggle. Set ONLY via UpdatePricingAnalysisCommand —
    /// the dedicated control on the pricing summary screen — never derived, and, as of the
    /// per-method flag landing, deliberately never force-written by a method-level save handler
    /// either (see the decision note on <see cref="ContributingMethodsUseSystemCalc"/>).
    /// It answers "was *the* headline figure (<see cref="FinalAppraisedValue"/>) calculated or
    /// typed", which is a different question from <see cref="PricingAnalysisMethod.UseSystemCalc"/>:
    /// several methods can each contribute to that figure (a Cost approach sums role-tagged
    /// methods; other approaches show several side by side), so the two can disagree — e.g. this
    /// stays true while one Cost component is manually overridden. Flipping a method's flag never
    /// writes this column, and setting this column never writes any method's flag.
    /// </summary>
    public bool UseSystemCalc { get; private set; } = true;

    /// <summary>
    /// Rollup rule for <see cref="PricingAnalysisMethod.UseSystemCalc"/>: FinalAppraisedValue is
    /// produced by the selected approach's selected method(s) (see
    /// <see cref="RollUpFinalFromSelectedApproach"/> / <see cref="PricingAnalysisApproach.SelectMethod"/>),
    /// so the headline figure is System-computed only when EVERY one of those contributing methods
    /// still is — one manually-overridden component is enough to make the number, as a whole,
    /// partly typed. An analysis with no selected approach, or an approach with no selected method,
    /// contributes nothing and reads as System (vacuously true), so a blank analysis never reads as
    /// manual before anyone has touched it.
    /// <para>
    /// Read-only — it does not read or write <see cref="UseSystemCalc"/>.
    /// </para>
    /// <para>
    /// DECISION (the four automatic save handlers — SetFinalValue, UpdateFinalValue,
    /// SetManualCostBreakdown, SaveComparativeAnalysis): they stamp
    /// <see cref="PricingAnalysisMethod.RecordCalcMode"/> on the one method they actually wrote, and
    /// stop force-writing <see cref="UseSystemCalc"/> entirely — they used to, under a
    /// "// TODO: Temporary" comment, back when the method-level flag did not exist and the group
    /// column was the only place to record "the user just typed something". That workaround is now
    /// actively wrong: SaveComparativeAnalysis unconditionally forced this column back to true on
    /// every recalculation, including one run against a method OTHER than the one an appraiser had
    /// just manually overridden via SetFinalValue — silently erasing the user's own toggle
    /// mid-session with no user action involved. This is the "check every sibling" test the
    /// abandoned branch reached for (there, only to decide whether to clear manual-evidence
    /// documents) generalized into one named rule instead: a caller that wants to keep this column
    /// truthful compares its own intent against this property explicitly rather than a handler
    /// blindly asserting one, and a document-cleanup path can use it the same way. Nothing currently
    /// calls it — the four handlers were the only candidates, and per this decision none of them do
    /// — so it exists for the report/FE and for whichever future caller needs the comparison.
    /// </para>
    /// <para>
    /// Transiently misleading mid-operation: <see cref="PricingAnalysisMethod.SetCalcMode"/>
    /// unselects the method it flips, and this property only counts selected methods — so reading
    /// it between a <c>SetCalcMode</c> call and whatever re-selects that method (or another) can
    /// answer System even though the analysis was just made manual, simply because nothing is
    /// selected yet. Callers must read this only after selection has settled for the unit of work,
    /// never in between.
    /// </para>
    /// </summary>
    public bool ContributingMethodsUseSystemCalc =>
        _approaches.FirstOrDefault(a => a.IsSelected) is not { } selectedApproach
        || selectedApproach.Methods.Where(m => m.IsSelected).All(m => m.UseSystemCalc);

    private readonly List<PricingAnalysisDocument> _documents = [];
    public IReadOnlyList<PricingAnalysisDocument> Documents => _documents.AsReadOnly();
    public string? Remark { get; private set; } = null!;

    private PricingAnalysis()
    {
    }

    // ── Factory methods ───────────────────────────────────────────────────────

    /// <summary>Creates a PricingAnalysis for a PropertyGroup.</summary>
    public static PricingAnalysis CreateForPropertyGroup(Guid propertyGroupId)
    {
        if (propertyGroupId == Guid.Empty)
            throw new ArgumentException("PropertyGroupId must not be empty.", nameof(propertyGroupId));

        return new PricingAnalysis
        {
            Id = Guid.CreateVersion7(),
            SubjectType = PricingAnalysisSubjectType.PropertyGroup,
            AnchorId = propertyGroupId,
            Status = "Draft"
        };
    }

    /// <summary>
    /// Creates a PricingAnalysis for a ProjectModel.
    /// FinalAppraisedValue on this analysis becomes the model's standard price.
    /// </summary>
    public static PricingAnalysis CreateForProjectModel(Guid projectModelId)
    {
        if (projectModelId == Guid.Empty)
            throw new ArgumentException("ProjectModelId must not be empty.", nameof(projectModelId));

        return new PricingAnalysis
        {
            Id = Guid.CreateVersion7(),
            SubjectType = PricingAnalysisSubjectType.ProjectModel,
            AnchorId = projectModelId,
            Status = "Draft"
        };
    }

    /// <summary>
    /// Creates a reference PricingAnalysis anchored to a non-group field.
    /// </summary>
    /// <param name="subjectType">One of the Ref subtypes (MachineryCostRef…ProfitRentRef).</param>
    /// <param name="anchorId">Owning entity id (e.g. AppraisalProperty id for machinery).</param>
    /// <param name="anchorRefKey">Optional sub-key within the anchor (e.g. room-type name).</param>
    /// <param name="hostMethodId">PricingAnalysisMethod that logically owns the field. Used for cleanup.</param>
    public static PricingAnalysis CreateForReference(
        PricingAnalysisSubjectType subjectType,
        Guid anchorId,
        string? anchorRefKey = null,
        Guid? hostMethodId = null)
    {
        if (subjectType is PricingAnalysisSubjectType.PropertyGroup or PricingAnalysisSubjectType.ProjectModel)
            throw new ArgumentException(
                "Use CreateForPropertyGroup / CreateForProjectModel for non-reference subject types.",
                nameof(subjectType));

        if (anchorId == Guid.Empty)
            throw new ArgumentException("AnchorId must not be empty.", nameof(anchorId));

        return new PricingAnalysis
        {
            Id = Guid.CreateVersion7(),
            SubjectType = subjectType,
            AnchorId = anchorId,
            AnchorRefKey = anchorRefKey,
            HostMethodId = hostMethodId,
            Status = "Draft"
        };
    }

    /// <summary>
    /// Creates a reference PricingAnalysis by deep-cloning a source method into a new "Market" approach.
    /// The clone is fully independent — editing it never touches the source.
    /// </summary>
    /// <param name="subjectType">One of the Ref subtypes (MachineryCostRef…ProfitRentRef). Must not be PropertyGroup/ProjectModel.</param>
    /// <param name="anchorId">Owning entity id for this reference.</param>
    /// <param name="hostMethodId">PricingAnalysisMethod that logically owns the field. Used for cleanup.</param>
    /// <param name="sourceMethod">The Cost-approach method to clone (WQS/SaleGrid/DirectComparison).</param>
    /// <param name="landAreaOverride">When set, overrides the cloned method's FinalValue.LandArea to this value while preserving LandValue.</param>
    public static PricingAnalysis CreateReferenceFromMethod(
        PricingAnalysisSubjectType subjectType,
        Guid anchorId,
        Guid? hostMethodId,
        PricingAnalysisMethod sourceMethod,
        decimal? landAreaOverride = null)
    {
        if (subjectType is PricingAnalysisSubjectType.PropertyGroup or PricingAnalysisSubjectType.ProjectModel)
            throw new ArgumentException(
                "Use CreateForPropertyGroup / CreateForProjectModel for non-reference subject types.",
                nameof(subjectType));

        if (anchorId == Guid.Empty)
            throw new ArgumentException("AnchorId must not be empty.", nameof(anchorId));

        var pa = new PricingAnalysis
        {
            Id = Guid.CreateVersion7(),
            SubjectType = subjectType,
            AnchorId = anchorId,
            AnchorRefKey = null,
            HostMethodId = hostMethodId,
            Status = "Draft"
        };

        var approach = PricingAnalysisApproach.Create(pa.Id, "Market");
        pa._approaches.Add(approach);

        var clonedMethod = approach.AttachClonedMethod(sourceMethod);

        // Override land area when a partial-land value is specified (DCF non-HBU land reference).
        // Keep the existing LandValue; only update LandArea.
        if (landAreaOverride.HasValue && clonedMethod.FinalValue is not null)
        {
            clonedMethod.FinalValue.SetLandAreaValues(
                landAreaOverride.Value,
                clonedMethod.FinalValue.LandValue ?? 0m);
        }

        return pa;
    }

    // ── Approach management ───────────────────────────────────────────────────

    public PricingAnalysisApproach AddApproach(string approachType, decimal? weight = null)
    {
        if (approachType != "Market" && approachType != "Cost" && approachType != "Income" && approachType != "Residual")
            throw new ArgumentException("ApproachType must be 'Market', 'Cost', 'Income', or 'Residual'");

        if (_approaches.Any(a => a.ApproachType == approachType))
            throw new InvalidOperationException($"Approach '{approachType}' already exists");

        var approach = PricingAnalysisApproach.Create(Id, approachType);
        _approaches.Add(approach);
        return approach;
    }

    public void StartProgress()
    {
        if (Status != "Draft")
            throw new InvalidOperationException($"Cannot start analysis in status '{Status}'");

        Status = "InProgress";
    }

    public void Complete(decimal appraisedValue)
    {
        if (Status != "InProgress")
            throw new InvalidOperationException($"Cannot complete analysis in status '{Status}'");

        SetFinalAppraisedValueInternal(appraisedValue);
        Status = "Completed";
    }

    public void SetFinalValues(decimal appraisedValue)
    {
        SetFinalAppraisedValueInternal(appraisedValue);
    }

    public void ClearFinalValues()
    {
        SetFinalAppraisedValueInternal(null);
    }

    /// <summary>
    /// Selects <paramref name="approachId"/> as the analysis's final approach, unselecting all
    /// others, and propagates its <c>ApproachValue</c> up to <see cref="FinalAppraisedValue"/> —
    /// even when that value is null — so the rollup never keeps a stale value from a previously
    /// selected approach.
    /// </summary>
    public void SelectApproach(Guid approachId)
    {
        var targetApproach = _approaches.FirstOrDefault(a => a.Id == approachId);

        if (targetApproach is null)
            throw new NotFoundException("PricingAnalysisApproach", approachId);

        RuleCheck.Valid()
            .AddErrorIf(
                targetApproach.Methods.All(m => !m.IsSelected),
                "Cannot select an approach that has no selected method.")
            .ThrowIfInvalid();

        foreach (var approach in _approaches)
        {
            if (approach.Id == targetApproach.Id)
                approach.Select();
            else
                approach.Unselect();
        }

        SetFinalAppraisedValueInternal(targetApproach.ApproachValue);
    }

    /// <summary>
    /// Selects <paramref name="methodId"/> as the primary method within its parent approach
    /// (setting all other methods in that approach as Alternative). If the parent approach is
    /// already the analysis's selected/final approach, also propagates the method's value up to
    /// <see cref="FinalAppraisedValue"/> — even when that value is null.
    /// </summary>
    public void SelectMethod(Guid methodId)
    {
        var parentApproach = _approaches.FirstOrDefault(a => a.Methods.Any(m => m.Id == methodId));

        if (parentApproach is null)
            throw new NotFoundException("PricingAnalysisMethod", methodId);

        parentApproach.SelectMethod(methodId);

        if (parentApproach.IsSelected)
            SetFinalAppraisedValueInternal(parentApproach.ApproachValue);
    }

    /// <summary>
    /// Applies a COMPLETE selection — the primary method for each listed approach plus the
    /// analysis's final approach — as one atomic operation, raising the final-value domain event
    /// exactly ONCE.
    /// <para>
    /// This is the aggregate-level equivalent of calling <see cref="SelectMethod"/> once per
    /// approach and then <see cref="SelectApproach"/>, which is what the pricing summary screen
    /// used to do over N+1 HTTP requests: N+1 transactions, up to two ValuationAnalyses recomputes
    /// per save, and a window where the method selections committed but the approach selection
    /// failed, leaving a half-applied selection. Every method selection here goes through the
    /// approach-level <see cref="PricingAnalysisApproach.SelectMethod"/>, which raises nothing —
    /// only the single <see cref="SetFinalAppraisedValueInternal"/> call at the end does.
    /// </para>
    /// <para>
    /// Validation is fully up front: nothing is mutated unless every approach and method resolves,
    /// so a bad payload cannot leave a partially-applied selection behind.
    /// </para>
    /// </summary>
    /// <param name="fullyDescribedApproachIds">
    /// Approaches whose method selection this payload states IN FULL: each is cleared before the
    /// payload is applied, so a method left out of <paramref name="selections"/> ends up
    /// deselected. Approaches not listed here keep whatever they had — which is what makes it safe
    /// for the summary screen to send only the approaches the appraiser touched. An empty list is
    /// the pre-existing behaviour exactly: purely additive, nothing ever deselected.
    /// <para>
    /// Without this, omission could not mean deselection: <see cref="PricingAnalysisApproach.SelectMethod"/>
    /// only clears siblings sharing the target's Role, so an unticked Cost method stayed selected
    /// server-side and came back on the next load. Clearing EVERY approach instead would have been
    /// wrong in the other direction — the payload does not mention untouched approaches at all, so
    /// that would silently wipe their selections.
    /// </para>
    /// </param>
    public void ApplySelection(
        IReadOnlyCollection<ApproachMethodSelection> selections,
        Guid finalApproachId,
        IReadOnlyCollection<Guid>? fullyDescribedApproachIds = null)
    {
        ArgumentNullException.ThrowIfNull(selections);

        var clearScope = fullyDescribedApproachIds is { Count: > 0 }
            ? fullyDescribedApproachIds.ToHashSet()
            : [];

        var finalApproach = _approaches.FirstOrDefault(a => a.Id == finalApproachId);

        if (finalApproach is null)
            throw new NotFoundException("PricingAnalysisApproach", finalApproachId);

        // ── Validate everything BEFORE mutating anything ──────────────────────────
        var resolved = new List<(PricingAnalysisApproach Approach, Guid MethodId)>(selections.Count);

        foreach (var selection in selections)
        {
            var approach = _approaches.FirstOrDefault(a => a.Id == selection.ApproachId);

            if (approach is null)
                throw new NotFoundException("PricingAnalysisApproach", selection.ApproachId);

            if (approach.Methods.All(m => m.Id != selection.MethodId))
                throw new NotFoundException("PricingAnalysisMethod", selection.MethodId);

            resolved.Add((approach, selection.MethodId));
        }

        // The final approach must end up with a selected method — either one this payload selects,
        // or one already selected from an earlier save. Checked before mutating so a rejected
        // payload changes nothing.
        //
        // An existing selection only counts when this payload is NOT about to clear it: for an
        // approach in the clear scope, the payload is the whole truth, so "it had one before" says
        // nothing about what it will have after. Reading the pre-clear state there would let a
        // payload that deselects the final approach's last method commit an analysis whose final
        // approach backs no method at all — the very state this rule exists to prevent.
        var finalApproachIsCleared = clearScope.Contains(finalApproachId);
        var finalApproachHasMethod =
            resolved.Any(r => r.Approach.Id == finalApproachId)
            || (!finalApproachIsCleared && finalApproach.Methods.Any(m => m.IsSelected));

        var check = RuleCheck.Valid()
            .AddErrorIf(
                !finalApproachHasMethod,
                "Cannot select an approach that has no selected method.");

        // ── No Cost component may be counted twice ────────────────────────────────
        // A Cost approach's value is the SUM of its selected methods (ComputeSelectedValue), and
        // a LandAndBuilding-role method already prices the building — so ticking it alongside a
        // separate Building-role method adds the building twice and inflates the group's
        // appraised value, which then flows to the report and to LOS/AS400.
        // Checked here on the payload's END state (see PricingAnalysisApproach.SelectionAfter) —
        // two methods sharing a role, or two DIFFERENT roles covering the same component.
        //
        // The screen disables Save on the same condition, and PricingAnalysisApproach.SelectMethod /
        // SetMethodRole enforce it on the single-method paths; this pre-check exists so a bad
        // payload is rejected before anything is mutated.
        //
        // Deliberately NOT rejecting a selected method whose Role is null, which the screen also
        // blocks. Role is newer than the data: analyses saved before it existed can legitimately
        // carry null, and refusing those would make them unsavable. Whether any such rows remain
        // is a question for a query against real data, not a guess to encode here.

        foreach (var approach in _approaches.Where(a => a.ApproachType == "Cost"))
        {
            // Same reasoning as finalApproachHasMethod above: for an approach in the clear scope
            // the payload is the complete truth, so what was selected before says nothing about
            // what will be selected after.
            var keepsExistingSelections = !clearScope.Contains(approach.Id);

            // Validate the END state, computed by the same steps the apply loop below takes
            // (same-role / linked / role-less deselection), in payload order — so a plain swap
            // (WQS → SAG for Land) from an older client is not rejected for a selection that
            // SelectMethod would have dropped anyway.
            var payloadIds = resolved.Where(r => r.Approach.Id == approach.Id).Select(r => r.MethodId).ToList();
            var endState = approach.SelectionAfter(payloadIds, keepsExistingSelections);

            // A payload entry dropped by a LATER entry (SAG then a LandAndBuilding WQS) would make
            // the result depend on payload order and silently discard a method the user ticked.
            if (payloadIds.Any(id => endState.All(m => m.Id != id)))
                check.AddError(
                    "Two selected methods in the Cost approach cover the same component. "
                    + "Select only one of them.");

            if (PricingAnalysisApproach.FindDoubleCountedComponent(endState) is { } dup)
                check.AddError(
                    $"{dup.Component} would be counted by {dup.Count} selected methods in the Cost "
                    + "approach. Deselect one of them, or change its role.");
        }

        check.ThrowIfInvalid();

        // ── Apply ─────────────────────────────────────────────────────────────────
        // Clear first, then select: within one approach the payload's entries are the complete
        // set, so anything it omits must end up unselected.
        foreach (var approach in _approaches)
            if (clearScope.Contains(approach.Id))
                approach.ClearMethodSelections();

        // End state already validated above; a per-step check could trip on an intermediate state.
        foreach (var (approach, methodId) in resolved)
            approach.SelectMethod(methodId, checkComponents: false);

        foreach (var approach in _approaches)
        {
            if (approach.Id == finalApproachId)
                approach.Select();
            else
                approach.Unselect();
        }

        // The ONLY event-raising call in this operation.
        SetFinalAppraisedValueInternal(finalApproach.ApproachValue);
    }

    /// <summary>
    /// A user flipping one method's calc mode — a deliberate deselection. The approach re-derives
    /// from what is left (possibly nothing), and when it is the selected approach FinalAppraisedValue
    /// follows it, null included, the same as <see cref="ApplySelection"/> after an untick.
    /// <see cref="RecalculateRollup"/> cannot do this: its guards keep the previous figure when
    /// nothing selected has a value, which here would be the value of the method just deselected.
    /// </summary>
    public void SetMethodCalcMode(Guid methodId, bool useSystemCalc)
    {
        var approach = _approaches.FirstOrDefault(a => a.Methods.Any(m => m.Id == methodId))
                       ?? throw new InvalidOperationException($"Method {methodId} not found in this pricing analysis");

        if (!approach.SetMethodCalcMode(methodId, useSystemCalc))
            return;

        if (approach.IsSelected && FinalAppraisedValue != approach.ApproachValue)
            SetFinalAppraisedValueInternal(approach.ApproachValue);
    }

    /// <summary>
    /// Single entry point for the method → approach → analysis rollup. Idempotent and null-safe:
    /// every approach re-derives from its selected method, then FinalAppraisedValue re-derives from
    /// the selected approach. Call this after ANY mutation that can change a MethodValue —
    /// including recalculations and deletions.
    /// </summary>
    public void RecalculateRollup()
    {
        foreach (var approach in _approaches)
            approach.SyncValueFromSelectedMethod();

        RollUpFinalFromSelectedApproach();
    }

    /// <summary>
    /// Rolls the selected approach's value up to <see cref="FinalAppraisedValue"/>. The second half
    /// of <see cref="RecalculateRollup"/>, which is now its only caller — it was public while
    /// UpdateApproach accepted a client-supplied ApproachValue and needed the roll-up without the
    /// method re-sync that would have clobbered it. Approach values are always derived from the
    /// selected method, so that path no longer exists.
    /// <para>
    /// An analysis with NO selected approach is left untouched — that is the manual final-value
    /// entry path (<see cref="SetFinalValues"/> via UpdatePricingAnalysis), which must not be nulled
    /// out. This mirrors <see cref="PricingAnalysisApproach.SyncValueFromSelectedMethod"/> at the
    /// approach level. The (expensive) summary recompute + integration event fires only when the
    /// rolled-up value actually changes.
    /// </para>
    /// </summary>
    private void RollUpFinalFromSelectedApproach()
    {
        var selected = _approaches.FirstOrDefault(a => a.IsSelected);

        // No selected approach (manual final-value entry path), or the selected approach has no
        // derivable value yet (its selected method is blank): leave FinalAppraisedValue untouched
        // rather than nulling a manually-set or previously-computed figure — which would drop the
        // group's contribution to 0. Explicit selection (SelectApproach) still clears via
        // SetFinalAppraisedValueInternal directly when the user deliberately switches approach.
        if (selected?.ApproachValue is null)
            return;

        // Only fire the (expensive) summary recompute + integration event when the value changes.
        if (FinalAppraisedValue == selected.ApproachValue)
            return;

        SetFinalAppraisedValueInternal(selected.ApproachValue);
    }

    private void SetFinalAppraisedValueInternal(decimal? value)
    {
        FinalAppraisedValue = value;

        switch (SubjectType)
        {
            case PricingAnalysisSubjectType.PropertyGroup when AnchorId.HasValue:
                // Triggers recalculation of the appraisal-level ValuationAnalysis summary.
                AddDomainEvent(new AppraisalFinalValuesChangedEvent(AnchorId.Value));
                break;

            case PricingAnalysisSubjectType.ProjectModel when AnchorId.HasValue:
                // Future subscribers can use this to propagate the model's standard price downstream.
                AddDomainEvent(new ProjectModelPricingFinalValueChangedEvent(Id, AnchorId.Value, value));
                break;

            // All reference subject types fire NO event — they are independent market references
            // and must not pollute the appraisal-level valuation rollup.
            default:
                break;
        }
    }

    public void SetUseSystemCalc(bool value)
    {
        UseSystemCalc = value;
    }

    /// <summary>
    /// Deep-clone for CI carry-forward — sets <see cref="Status"/> to "Draft" so the appraiser
    /// must re-confirm the valuation against the new construction snapshot. Carries forward
    /// FinalAppraisedValue and the entire Approaches/Methods chain (including 1:1 method analyses
    /// and AppraisalComparable references via global MarketComparableId).
    /// </summary>
    public static PricingAnalysis CloneForGroup(
        PricingAnalysis source,
        Guid newPropertyGroupId,
        IReadOnlyDictionary<Guid, Guid>? propertyIdMap = null)
    {
        var clone = new PricingAnalysis
        {
            Id = Guid.CreateVersion7(),
            SubjectType = PricingAnalysisSubjectType.PropertyGroup,
            AnchorId = newPropertyGroupId,
            Status = "Draft",
            FinalAppraisedValue = source.FinalAppraisedValue,
            UseSystemCalc = source.UseSystemCalc
        };

        foreach (var a in source.Approaches)
            clone._approaches.Add(PricingAnalysisApproach.CloneForAnalysis(a, clone.Id, propertyIdMap));

        // Deliberately does NOT raise AppraisalFinalValuesChangedEvent. That event dispatches
        // PRE-save (DispatchDomainEventInterceptor), when this clone is still Added and invisible to
        // the SQL sum in the summary handler — emitting here wrote AppraisedValue = 0 on CI copy, and
        // fired N times for N groups. The ValuationAnalyses summary is instead recomputed ONCE,
        // POST-save, by AppraisalCreationService via AppraisalValuationSummaryService.RecomputeAsync.
        // FinalAppraisedValue still carries forward verbatim (set in the initializer above, bypassing
        // SetFinalAppraisedValueInternal).

        return clone;
    }

    // Documents management
    public PricingAnalysisDocument AddDocument(PricingAnalysisDocumentData data)
    {
        RuleCheck.Valid()
             .AddErrorIf(
                 data.DocumentId.HasValue && _documents.Any(d => d.DocumentId == data.DocumentId),
                 $"Document '{data.DocumentId}' is already linked to this pricing analysis.")
             .ThrowIfInvalid();

        var document = PricingAnalysisDocument.Create(Id, data);

        _documents.Add(document);

        if (data.DocumentId.HasValue)
            AddDomainEvent(new DocumentLinkedEvent(Id, data.DocumentId.Value));

        return document;
    }

    public void UpdateDocument(Guid documentId, PricingAnalysisDocumentData data)
    {
        var document = _documents.FirstOrDefault(d => d.Id == documentId);

        RuleCheck.Valid()
            .AddErrorIf(document is null, $"Document with id '{documentId}' not found in this pricing analysis.")
            .AddErrorIf(
                 data.DocumentId.HasValue && _documents.Any(d => d.DocumentId == data.DocumentId),
                 $"Document '{data.DocumentId}' is already linked to this pricing analysis.")
            .ThrowIfInvalid();

        var (previousDocId, newDocId) = document!.Update(data);

        // Fire appropriate domain events based on document changes
        if (previousDocId.HasValue && newDocId.HasValue)
            AddDomainEvent(new DocumentUpdatedEvent(Id, previousDocId.Value, newDocId.Value));
        else if (!previousDocId.HasValue && newDocId.HasValue)
            AddDomainEvent(new DocumentLinkedEvent(Id, newDocId.Value));
        else if (previousDocId.HasValue && !newDocId.HasValue)
            AddDomainEvent(new DocumentUnlinkedEvent(Id, previousDocId.Value));
    }

    public void RemoveDocument(Guid documentId)
    {
        var document = _documents.FirstOrDefault(d => d.Id == documentId);

        RuleCheck.Valid()
            .AddErrorIf(document is null, $"Document with id '{documentId}' not found in this pricing analysis.")
            .ThrowIfInvalid();

        _documents.Remove(document!);

        if (document!.DocumentId.HasValue)
            AddDomainEvent(new DocumentUnlinkedEvent(Id, document.DocumentId.Value));
    }

    public PricingAnalysisDocument? GetDocument(Guid documentId)
    {
        return _documents.FirstOrDefault(d => d.Id == documentId);
    }

    public bool HasDocument(Guid documentId)
    {
        return _documents.Any(d => d.Id == documentId);
    }

    public void SetRemark(string? remark)
    {
        Remark = remark;
    }

    /// <summary>
    /// Removes a SELECTED method from a Cost approach that still has other selected components
    /// (multi-select Cost: Land + Building + Machinery). Unlike the exclusive approaches, removing
    /// one component must not unselect the approach or null FinalAppraisedValue: the approach
    /// re-derives from what stays selected and the final value follows it. A removed
    /// LandAndBuilding method (linked or merely tagged) first reverts to Land, so its BuildingCost
    /// re-enters the rollup instead of the building silently disappearing with it.
    /// Returns false (nothing done) when this path does not apply — the caller keeps the
    /// exclusive-approach behaviour (unselect approach, clear final value).
    /// </summary>
    public bool TryRemoveSelectedCostComponent(Guid approachId, Guid methodId)
    {
        var approach = _approaches.First(a => a.Id == approachId);
        var method = approach.Methods.First(m => m.Id == methodId);

        if (approach.ApproachType != "Cost" || !method.IsSelected)
            return false;

        // Linked or merely tagged LandAndBuilding: bring the BuildingCost method back first.
        approach.RevertToLand(methodId);

        if (!approach.Methods.Any(m => m.Id != methodId && m.IsSelected))
            return false;

        approach.RemoveMethod(methodId);
        RederiveFromSelection(approach);
        return true;
    }

    /// <summary>
    /// After one component of a multi-select Cost approach was cleared (reset) while others stay
    /// selected: re-derive the approach from what is still selected and let the final value follow,
    /// exactly like <see cref="TryRemoveSelectedCostComponent"/>. Returns false when this does not
    /// apply (not Cost, or nothing left selected) — the caller then clears approach and final value.
    /// </summary>
    public bool TryRederiveCostAfterComponentReset(Guid approachId)
    {
        var approach = _approaches.First(a => a.Id == approachId);
        if (approach.ApproachType != "Cost" || !approach.Methods.Any(m => m.IsSelected))
            return false;

        RederiveFromSelection(approach);
        return true;
    }

    private void RederiveFromSelection(PricingAnalysisApproach approach)
    {
        approach.RecomputeValueFromSelection();

        if (approach.IsSelected && FinalAppraisedValue != approach.ApproachValue)
            SetFinalAppraisedValueInternal(approach.ApproachValue);
    }

    public void RemoveApproach(Guid approachId)
    {
        var approach = _approaches.FirstOrDefault(m => m.Id == approachId);
        if (approach is null)
            throw new InvalidOperationException($"Approach with ID {approachId} not found in pricing analysis.");

        _approaches.Remove(approach);
    }
}
