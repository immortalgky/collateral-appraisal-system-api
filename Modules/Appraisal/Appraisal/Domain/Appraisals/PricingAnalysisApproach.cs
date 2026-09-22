namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Approach selection per pricing analysis (Market, Cost, Income).
/// </summary>
public class PricingAnalysisApproach : Entity<Guid>
{
    private readonly List<PricingAnalysisMethod> _methods = [];
    public IReadOnlyList<PricingAnalysisMethod> Methods => _methods.AsReadOnly();

    public Guid PricingAnalysisId { get; private set; }

    // Approach
    public string ApproachType { get; private set; } = null!; // Market, Cost, Income
    public decimal? ApproachValue { get; private set; }
    public bool IsSelected { get; private set; }

    private PricingAnalysisApproach()
    {
        // For EF Core
    }

    public static PricingAnalysisApproach Create(
        Guid pricingAnalysisId,
        string approachType)
    {
        return new PricingAnalysisApproach
        {
            Id = Guid.CreateVersion7(),
            PricingAnalysisId = pricingAnalysisId,
            ApproachType = approachType,
            IsSelected = false
        };
    }

    /// <summary>Deep-clone for CI carry-forward — rebuilds Methods chain. <paramref name="propertyIdMap"/> is threaded into MachineCostItem cloning.</summary>
    public static PricingAnalysisApproach CloneForAnalysis(
        PricingAnalysisApproach source,
        Guid newAnalysisId,
        IReadOnlyDictionary<Guid, Guid>? propertyIdMap = null)
    {
        var clone = new PricingAnalysisApproach
        {
            Id = Guid.CreateVersion7(),
            PricingAnalysisId = newAnalysisId,
            ApproachType = source.ApproachType,
            ApproachValue = source.ApproachValue,
            IsSelected = source.IsSelected
        };

        var methodIdMap = new Dictionary<Guid, Guid>();
        foreach (var m in source.Methods)
        {
            var clonedMethod = PricingAnalysisMethod.CloneForApproach(m, clone.Id, propertyIdMap);
            clone._methods.Add(clonedMethod);
            methodIdMap[m.Id] = clonedMethod.Id;
        }

        // LinkedMethodId points at a sibling method's Id, meaningless until that sibling has its own
        // new Id — remap in a second pass now that every clone in this approach exists. A link whose
        // target was not cloned alongside it (should not happen — a WQS/SAG/DC and its linked
        // BuildingCost always live in the same approach) is dropped rather than carried forward
        // pointing at the wrong appraisal's method.
        foreach (var m in source.Methods)
        {
            if (m.LinkedMethodId is not { } sourceLinkedId
                || !methodIdMap.TryGetValue(sourceLinkedId, out var newLinkedId))
                continue;

            var clonedMethod = clone._methods.First(x => x.Id == methodIdMap[m.Id]);
            clonedMethod.SetLinkedMethod(newLinkedId);
        }

        return clone;
    }

    /// <summary>
    /// Clones <paramref name="source"/> into this approach and returns the cloned method.
    /// Used by <see cref="PricingAnalysis.CreateReferenceFromMethod"/> to attach a deep-copied
    /// method without going through the factory guard in <see cref="AddMethod"/>.
    /// </summary>
    public PricingAnalysisMethod AttachClonedMethod(PricingAnalysisMethod source)
    {
        var clone = PricingAnalysisMethod.CloneForApproach(source, Id);
        _methods.Add(clone);
        return clone;
    }

    public PricingAnalysisMethod AddMethod(string methodType, string status = "Selected")
    {
        var method = PricingAnalysisMethod.Create(Id, methodType, status);

        // Default Role for a Cost-approach method — see PricingAnalysisMethod.Role. BuildingCost is
        // always the Building component and MachineryCost always Machinery; everything else starts
        // as Land ("include building" is off by default) and moves to LandAndBuilding only through
        // LinkOrCreateBuildingCostMethod. Non-Cost approaches never tag a role.
        if (ApproachType == "Cost")
            method.SetRole(methodType switch
            {
                "BuildingCost" => "Building",
                "MachineryCost" => "Machinery",
                _ => "Land"
            });

        _methods.Add(method);
        return method;
    }

    /// <summary>
    /// "Include building" ON for a Land-role WQS/SaleGrid/DirectComparison method under this Cost
    /// approach: links it to this approach's BuildingCost method, creating one if none exists yet,
    /// and re-tags the source method Role=LandAndBuilding. <paramref name="buildingCostValue"/> is
    /// the group's building-cost total — the caller computes it (PricingPropertyDataService.
    /// GetTotalBuildingCostAsync, the same aggregation the manual Cost-breakdown save already uses)
    /// since this entity has no database access.
    /// <para>
    /// The BuildingCost method is deselected here: the source method's own total already folds its
    /// value in (see the save handler, which snapshots it onto the source's own BuildingValue), so
    /// counting the BuildingCost method as a second selected Cost component would double it.
    /// </para>
    /// </summary>
    public PricingAnalysisMethod LinkOrCreateBuildingCostMethod(Guid sourceMethodId, decimal buildingCostValue)
    {
        var sourceMethod = _methods.FirstOrDefault(m => m.Id == sourceMethodId)
                           ?? throw new InvalidOperationException($"Method {sourceMethodId} not found in this approach");

        var buildingCostMethod = _methods.FirstOrDefault(m => m.MethodType == "BuildingCost");
        if (buildingCostMethod is null)
        {
            // Creating it: seed from the group's building-cost total, since there is no figure of
            // record yet. "PerUnit" = whole-unit lumpsum (see PricingAnalysisMethod.UnitType
            // remarks) — the same literal MachineryCostCalculationService uses, not the
            // Domain.Services.PricingUnit helper, to avoid a Domain.Appraisals ->
            // Domain.Services dependency for one constant.
            buildingCostMethod = AddMethod("BuildingCost", "Selected");
            buildingCostMethod.SetFinalValue(PricingFinalValue.Create(buildingCostMethod.Id, buildingCostValue));
            buildingCostMethod.SetValue(buildingCostValue, null, "PerUnit");
        }
        else if (buildingCostMethod.MethodValue is null or 0m)
        {
            // Exists but was never priced — a method added from the selection board and never
            // opened. There is no figure to protect here, so seed it exactly as a fresh one.
            buildingCostMethod.SetValue(buildingCostValue, null, "PerUnit");
            if (buildingCostMethod.FinalValue is null)
                buildingCostMethod.SetFinalValue(PricingFinalValue.Create(buildingCostMethod.Id, buildingCostValue));
            else
                buildingCostMethod.FinalValue.UpdateFinalValue(buildingCostValue);
        }
        // An existing BuildingCost method that HOLDS a value keeps it — this method links, it does
        // not re-price. The Building Cost screen's "มูลค่าตามวิธี" is an appraiser-editable figure,
        // and this path used to overwrite both it and the FinalValue row with a freshly recomputed
        // roll-up on every save of the linked WQS/SAG/DC method. An appraiser who adjusted the
        // building value, then saved the market method that includes it, silently lost the
        // adjustment — the figure reverted to the schedule total with nothing on screen to say so.
        //
        // The cost of not recomputing is a stale link: if the building schedule changes afterwards,
        // this method keeps the older figure until someone opens the Building Cost screen. That is
        // the deliberate trade — a number the appraiser set is never overwritten without them.
        // Note the split above: "don't overwrite what a person set" is not the same rule as
        // "never fill an empty one", and collapsing the two leaves the Cost rollup counting zero.

        buildingCostMethod.SetRole("Building");

        sourceMethod.SetRole("LandAndBuilding");
        sourceMethod.SetLinkedMethod(buildingCostMethod.Id);

        // Excluded from the rollup while linked — see the XML remarks above. Only when the source
        // is itself selected: linking an UNselected method (the appraiser saving an alternative
        // they are not using) must not silently drop the building from a rollup that counts it
        // through the selected BuildingCost method. Selecting the source later deselects its
        // linked BuildingCost (SelectMethod / DeselectedBySelecting).
        if (sourceMethod.IsSelected)
            buildingCostMethod.SetAsUnselected();

        // sourceMethod's Role just changed without going through SelectMethod's deselect logic —
        // another already-selected method could now cover the same component (a second
        // LandAndBuilding, or a Land method next to this one). Reject rather than double-count.
        EnsureNoComponentCountedTwice();

        return buildingCostMethod;
    }

    /// <summary>
    /// "Include building" OFF: unlinks <paramref name="sourceMethodId"/>, reverts it to Role=Land,
    /// and re-selects the BuildingCost method (if it still exists) so it re-enters the Cost rollup
    /// as its own line item. The BuildingCost record itself is never deleted — only its selection
    /// and its source's Role/link change — so re-ticking later finds it again rather than recreating it.
    /// </summary>
    public void UnlinkBuildingCostMethod(Guid sourceMethodId)
    {
        var sourceMethod = _methods.FirstOrDefault(m => m.Id == sourceMethodId)
                           ?? throw new InvalidOperationException($"Method {sourceMethodId} not found in this approach");

        // Re-select the BuildingCost method only if this source was the selected method carrying
        // the building in the rollup, and no other selected method still folds the same
        // BuildingCost in. Unlinking an unselected alternative must not add the building on top of
        // a selected LandAndBuilding method that still includes it.
        if (sourceMethod.LinkedMethodId is { } linkedId
            && sourceMethod.IsSelected
            && !_methods.Any(m => m.Id != sourceMethod.Id && m.IsSelected && m.LinkedMethodId == linkedId))
        {
            var buildingCostMethod = _methods.FirstOrDefault(m => m.Id == linkedId);
            buildingCostMethod?.SetAsSelected();
        }

        sourceMethod.SetRole("Land");
        sourceMethod.SetLinkedMethod(null);

        EnsureNoComponentCountedTwice();
    }

    /// <summary>
    /// Flips one method's calc mode (<see cref="PricingAnalysisMethod.SetCalcMode"/>: unselect +
    /// clear value) and, when that deselected a method, re-derives <see cref="ApproachValue"/> from
    /// what is still selected. Assigned directly, like <see cref="ClearMethodSelections"/> — NOT via
    /// <see cref="SyncValueFromSelectedMethod"/>, whose early return (nothing selected has a value)
    /// would leave the total of a method that is no longer selected in place.
    /// Returns whether ApproachValue was re-derived.
    /// </summary>
    internal bool SetMethodCalcMode(Guid methodId, bool useSystemCalc)
    {
        var method = _methods.FirstOrDefault(m => m.Id == methodId)
                     ?? throw new InvalidOperationException($"Method {methodId} not found in this approach");

        var wasSelected = method.IsSelected;
        method.SetCalcMode(useSystemCalc);

        // An unselected method never fed the approach, and SetCalcMode no-ops on an unchanged flag.
        if (!wasSelected || method.IsSelected)
            return false;

        ApproachValue = ComputeSelectedValue();
        return true;
    }

    /// <summary>
    /// Re-tags one method's Role through the approach, so the one-selected-per-role rule still
    /// holds — setting <see cref="PricingAnalysisMethod.Role"/> on the method directly (as
    /// UpdateMethod used to) let a PUT leave two selected methods covering the same component.
    /// Changes no selection, so a linked BuildingCost method's deliberate deselection is untouched.
    /// </summary>
    public void SetMethodRole(Guid methodId, string role)
    {
        var method = _methods.FirstOrDefault(m => m.Id == methodId)
                     ?? throw new NotFoundException("PricingAnalysisMethod", methodId);

        // Role only means something inside the Cost rollup (ComputeSelectedValue sums by it there).
        if (ApproachType != "Cost")
            throw new DomainException("A method role can only be set on a Cost approach method.");

        // Moving OFF LandAndBuilding: the method's value may still fold the building in (and a
        // linked one keeps BuildingCost deselected) — undo that the same way include-building off
        // does, or selecting BuildingCost afterwards would count the building twice.
        if (method.Role == "LandAndBuilding" && role != "LandAndBuilding")
            RevertToLand(methodId);

        method.SetRole(role);
        EnsureNoComponentCountedTwice();
    }

    /// <summary>
    /// Returns a method that no longer folds the building in to plain Land. A linked method goes
    /// through <see cref="UnlinkBuildingCostMethod"/>. An UNLINKED LandAndBuilding method (the data
    /// fix tags one when the group had no building total to link to) is re-tagged Land and, like
    /// Unlink, brings the BuildingCost method back into the rollup when this method is selected and
    /// nothing else selected covers the building — otherwise the building would silently drop out.
    /// No-op for a method already on another role.
    /// </summary>
    public void RevertToLand(Guid methodId)
    {
        var method = _methods.FirstOrDefault(m => m.Id == methodId)
                     ?? throw new NotFoundException("PricingAnalysisMethod", methodId);

        if (method.LinkedMethodId.HasValue)
        {
            UnlinkBuildingCostMethod(methodId);
            return;
        }

        if (method.Role != "LandAndBuilding")
            return;

        method.SetRole("Land");

        var buildingCovered = _methods.Any(m => m.Id != methodId && m.IsSelected
                                                && RoleCoveredComponents(m.Role).Contains("Building"));
        if (method.IsSelected && !buildingCovered)
            _methods.FirstOrDefault(m => m.MethodType == "BuildingCost")?.SetAsSelected();

        EnsureNoComponentCountedTwice();
    }

    /// <summary>
    /// Which Cost components (Land / Building / Machinery) a role's value already contains. A
    /// LandAndBuilding method prices both, so it collides with a selected Land OR Building method.
    /// The single copy — PricingAnalysis.ApplySelection's pre-check uses it too.
    /// </summary>
    internal static IEnumerable<string> RoleCoveredComponents(string? role) => role switch
    {
        "Land" => ["Land"],
        "Building" => ["Building"],
        "Machinery" => ["Machinery"],
        "LandAndBuilding" => ["Land", "Building"],
        _ => [],
    };

    /// <summary>
    /// The one rule for what selecting <paramref name="target"/> unselects. Non-Cost approaches (and
    /// a role-less Cost method) are exclusive: every sibling. A role-tagged Cost method unselects
    /// the role-tagged siblings it fully replaces — every component they cover, it covers too. So a
    /// same-role method swaps, and a LandAndBuilding method also replaces the Land method and the
    /// (linked) BuildingCost method. A sibling covering something the target does NOT (selecting
    /// Building while a LandAndBuilding method is selected) is left alone, so the caller's
    /// component check rejects it instead of silently dropping the land. Role-less siblings are
    /// left alone too, as before (older rows predate Role).
    /// </summary>
    private bool DeselectedBySelecting(PricingAnalysisMethod target, PricingAnalysisMethod other)
    {
        if (other.Id == target.Id) return false;
        if (ApproachType != "Cost" || target.Role is null) return true;
        if (other.Role is null) return false;

        var targetCovers = RoleCoveredComponents(target.Role).ToHashSet();
        return RoleCoveredComponents(other.Role).All(targetCovers.Contains);
    }

    /// <summary>
    /// The methods that would be selected after selecting <paramref name="methodIds"/> in order,
    /// starting from the current selection or from none — the same steps <see cref="SelectMethod"/>
    /// takes, without mutating anything. Lets ApplySelection validate a payload's end state.
    /// </summary>
    internal IReadOnlyList<PricingAnalysisMethod> SelectionAfter(IEnumerable<Guid> methodIds, bool keepExisting)
    {
        var selected = keepExisting ? _methods.Where(m => m.IsSelected).ToList() : [];
        foreach (var id in methodIds)
        {
            var target = _methods.First(m => m.Id == id);
            selected.RemoveAll(m => DeselectedBySelecting(target, m));
            if (!selected.Contains(target)) selected.Add(target);
        }
        return selected;
    }

    /// <summary>
    /// The component (Land / Building / Machinery) that more than one of <paramref name="selected"/>
    /// would count in a Cost rollup, with how many — or null. A null Role covers nothing.
    /// </summary>
    internal static (string Component, int Count)? FindDoubleCountedComponent(IEnumerable<PricingAnalysisMethod> selected)
    {
        var duplicated = selected
            .SelectMany(m => RoleCoveredComponents(m.Role))
            .GroupBy(c => c)
            .FirstOrDefault(g => g.Count() > 1);
        return duplicated is null ? null : (duplicated.Key, duplicated.Count());
    }

    /// <summary>
    /// Rejects a Cost approach whose SELECTED methods would count one component twice — two same-role
    /// methods, or two different roles covering the same component. A null Role covers nothing
    /// (older rows predate Role; see ApplySelection's remarks). Thrown, not silently resolved: there
    /// is no "which one did the user mean" to fall back on.
    /// </summary>
    private void EnsureNoComponentCountedTwice()
    {
        if (ApproachType != "Cost") return;

        if (FindDoubleCountedComponent(_methods.Where(m => m.IsSelected)) is { } dup)
            throw new DomainException(
                $"{dup.Component} would be counted by {dup.Count} selected methods in the Cost "
                + "approach. Deselect one of them, or change its role.");
    }

    public void SetValue(decimal value)
    {
        ApproachValue = value;
    }

    public void ClearValue()
    {
        ApproachValue = null;
    }

    /// <summary>
    /// Selects <paramref name="methodId"/> within this approach.
    /// <para>
    /// Every approach type except Cost keeps the original rule: exactly one selected method, so
    /// selecting one deselects every sibling. A Cost approach is role-scoped instead — selecting a
    /// method only deselects siblings that share its <see cref="PricingAnalysisMethod.Role"/>, so
    /// e.g. a Land-role method and a Building-role method can be selected at the same time (needed
    /// so a linked BuildingCost method can sit alongside its WQS/SAG/DC, or re-enter the formula on
    /// its own once "include building" is unticked — see <see cref="UnlinkBuildingCostMethod"/>).
    /// A role-less method under Cost (should not normally happen) falls back to the exclusive rule.
    /// </para>
    /// Syncs <see cref="ApproachValue"/> afterwards — even when the target's value is null — so the
    /// approach never keeps a stale value left over from a previously selected method.
    /// </summary>
    /// <param name="checkComponents">False only from PricingAnalysis.ApplySelection, which has
    /// already validated the FINAL state of the whole payload (via <see cref="SelectionAfter"/>);
    /// checking after each step would reject valid payloads on an intermediate state.</param>
    public void SelectMethod(Guid methodId, bool checkComponents = true)
    {
        var targetMethod = _methods.FirstOrDefault(m => m.Id == methodId);

        if (targetMethod is null)
            throw new NotFoundException("PricingAnalysisMethod", methodId);

        targetMethod.SetAsSelected();

        foreach (var method in _methods.Where(m => DeselectedBySelecting(targetMethod, m)).ToList())
            method.SetAsUnselected();

        // Same-role siblings were just deselected; a DIFFERENT role covering the same component
        // (Building next to a selected LandAndBuilding) was not — the single-select endpoint must
        // not count the building twice any more than ApplySelection may.
        if (checkComponents)
            EnsureNoComponentCountedTwice();

        // Deliberate user selection: adopt the resulting total VERBATIM, null/zero included — see
        // SyncValueFromSelectedMethod's remarks for why this must NOT reuse that null-skipping logic.
        ApproachValue = ComputeSelectedValue();
    }

    /// <summary>
    /// Drops every method selection on this approach and re-derives <see cref="ApproachValue"/>
    /// from what is left — which, immediately after this call, is nothing, so the value becomes
    /// null until the caller selects again.
    /// <para>
    /// Exists because <see cref="SelectMethod"/> can only ever ADD to a Cost approach's selection:
    /// it clears siblings sharing the target's Role, so selecting Land leaves a selected Building
    /// method alone (correctly — they are different components). A payload that simply omits a
    /// method therefore cannot express "this one is no longer selected", which is why unticking a
    /// method on the summary screen never reached the database. <see cref="ApplySelection"/> calls
    /// this first for the approaches whose selection the payload fully describes, making omission
    /// mean deselection for exactly those approaches and nothing else.
    /// </para>
    /// <para>
    /// The value must be written here rather than left to <see cref="SyncValueFromSelectedMethod"/>:
    /// that one deliberately returns early when nothing selected has a value, to stop a
    /// not-yet-computed method zeroing a good total — so after a clear it would leave the previous
    /// total in place, exactly the stale figure this operation exists to remove.
    /// </para>
    /// </summary>
    internal void ClearMethodSelections()
    {
        foreach (var method in _methods)
            method.SetAsUnselected();

        ApproachValue = ComputeSelectedValue();
    }

    /// <summary>
    /// Re-derives <see cref="ApproachValue"/> from this approach's selected method(s), but only when
    /// there is at least one selected method with a value. An approach with NO selected method, or
    /// whose only selected method has no value yet (e.g. incomplete comparables → no RSQ result), is
    /// left untouched: this restores the old per-handler <c>method.MethodValue.HasValue</c> guard so
    /// a not-yet-computed method value never clobbers a previously-computed approach total (which
    /// would drop the group's contribution to 0).
    /// <para>
    /// Contrast <see cref="SelectMethod"/>, which adopts the recomputed total verbatim (null/zero
    /// included): a deliberate selection must not leave the previous total behind, whereas a
    /// recalculation must not zero a good total with a method that has not been computed.
    /// </para>
    /// </summary>
    internal void SyncValueFromSelectedMethod()
    {
        var hasSelectedValue = _methods.Any(m => m.IsSelected && m.MethodValue.HasValue);
        if (!hasSelectedValue) return;

        ApproachValue = ComputeSelectedValue();
    }

    /// <summary>
    /// A Cost approach's value is the sum of its selected methods' MethodValue — each selected
    /// method carries a distinct Role (SelectMethod enforces at most one selected method per role),
    /// so this can never double-count a component. Every other approach type still has exactly one
    /// selected method, so the sum degenerates to that method's own value, same as before.
    /// Methods with no value yet (not selected, or selected but not computed) simply contribute 0.
    /// </summary>
    private decimal? ComputeSelectedValue()
    {
        var selected = _methods.Where(m => m.IsSelected).ToList();
        if (selected.Count == 0) return null;

        if (ApproachType != "Cost")
            return selected[0].MethodValue;

        return selected.Sum(m => m.MethodValue ?? 0m);
    }

    public void Select()
    {
        IsSelected = true;
    }

    public void Unselect()
    {
        IsSelected = false;
    }

    /// <summary>
    /// Re-derives <see cref="ApproachValue"/> from the current selection, verbatim — for a
    /// deliberate change to the selection (a component removed), like <see cref="ClearMethodSelections"/>.
    /// </summary>
    internal void RecomputeValueFromSelection()
    {
        ApproachValue = ComputeSelectedValue();
    }

    /// <summary>
    /// Removes <paramref name="methodId"/> from this approach. The self-referencing LinkedMethodId FK
    /// is NO ACTION at the database (SQL Server rejects any cascading action on a self-reference), so
    /// any sibling still linked to this method must be unlinked first or SaveChanges hits the FK.
    /// </summary>
    public void RemoveMethod(Guid methodId)
    {
        var method = _methods.FirstOrDefault(m => m.Id == methodId);
        if (method is null)
            throw new InvalidOperationException($"Method with ID {methodId} not found in approach.");

        foreach (var sibling in _methods)
            if (sibling.LinkedMethodId == methodId)
                sibling.SetLinkedMethod(null);

        _methods.Remove(method);
    }
}