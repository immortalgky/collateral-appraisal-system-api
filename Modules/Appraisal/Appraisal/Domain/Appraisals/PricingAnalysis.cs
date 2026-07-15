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

    // Use system pricing calculation
    public bool UseSystemCalc { get; private set; } = true;

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

        // Trigger ValuationAnalyses recalc if a final value carries over. The handler subscribes
        // to AppraisalFinalValuesChangedEvent and sums all PricingAnalyses.FinalAppraisedValue
        // for the appraisal — emitting on every cloned PA with a non-null value backfills the total.
        if (clone.FinalAppraisedValue.HasValue)
            clone.AddDomainEvent(new AppraisalFinalValuesChangedEvent(newPropertyGroupId));

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
}
