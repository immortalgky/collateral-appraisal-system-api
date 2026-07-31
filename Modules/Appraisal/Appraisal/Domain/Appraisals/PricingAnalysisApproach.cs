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

        foreach (var m in source.Methods)
            clone._methods.Add(PricingAnalysisMethod.CloneForApproach(m, clone.Id, propertyIdMap));

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
        _methods.Add(method);
        return method;
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
    /// Selects <paramref name="methodId"/> as the primary method within this approach, setting
    /// all other methods here as Alternative, and syncs <see cref="ApproachValue"/> to the
    /// newly-selected method's value — even when that value is null — so the approach never
    /// keeps a stale value left over from a previously selected method.
    /// </summary>
    public void SelectMethod(Guid methodId)
    {
        var targetMethod = _methods.FirstOrDefault(m => m.Id == methodId);

        if (targetMethod is null)
            throw new NotFoundException("PricingAnalysisMethod", methodId);

        targetMethod.SetAsSelected();

        foreach (var method in _methods)
        {
            if (method.Id != methodId)
                method.SetAsUnselected();
        }

        // Deliberate user selection: adopt the target method's value VERBATIM, null included.
        // Deliberately not SyncValueFromSelectedMethod() — its null-skip is right for the
        // recalculation path (don't let a not-yet-computed method zero a good total) but wrong
        // here: skipping would leave the PREVIOUS method's number on an approach whose primary
        // method the user just changed, and PricingAnalysis.SelectMethod would then roll that
        // stale figure into FinalAppraisedValue.
        ApproachValue = targetMethod.MethodValue;
    }

    /// <summary>
    /// Re-derives <see cref="ApproachValue"/> from this approach's selected method, but only when
    /// that method actually has a value. An approach with NO selected method, or whose selected
    /// method has no value yet (e.g. incomplete comparables → no RSQ result), is left untouched:
    /// this restores the old per-handler <c>method.MethodValue.HasValue</c> guard so a not-yet-computed
    /// method value never clobbers a previously-computed approach total (which would drop the group's
    /// contribution to 0).
    /// <para>
    /// Contrast <see cref="SelectMethod"/>, which adopts the target method's value verbatim
    /// (null included): a deliberate selection must not leave the previous method's number behind,
    /// whereas a recalculation must not zero a good total with a method that has not been computed.
    /// </para>
    /// </summary>
    internal void SyncValueFromSelectedMethod()
    {
        var selected = _methods.FirstOrDefault(m => m.IsSelected);
        if (selected?.MethodValue is null) return;

        ApproachValue = selected.MethodValue;
    }

    public void Select()
    {
        IsSelected = true;
    }

    public void Unselect()
    {
        IsSelected = false;
    }

    public void RemoveMethod(Guid methodId)
    {
        var method = _methods.FirstOrDefault(m => m.Id == methodId);
        if (method is null)
            throw new InvalidOperationException($"Method with ID {methodId} not found in approach.");

        _methods.Remove(method);
    }
}