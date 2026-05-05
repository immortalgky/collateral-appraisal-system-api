namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Discriminates whether this PricingAnalysis belongs to a PropertyGroup or a ProjectModel.
/// </summary>
public enum PricingAnalysisSubjectType
{
    PropertyGroup = 0,
    ProjectModel = 1
}

/// <summary>
/// Pricing analysis container — 1:1 with either a PropertyGroup or a ProjectModel.
/// Exactly one of PropertyGroupId / ProjectModelId is non-null (enforced by domain factory and DB CHECK constraint).
/// </summary>
public class PricingAnalysis : Aggregate<Guid>
{
    private readonly List<PricingAnalysisApproach> _approaches = [];
    public IReadOnlyList<PricingAnalysisApproach> Approaches => _approaches.AsReadOnly();

    public PricingAnalysisSubjectType SubjectType { get; private set; }
    public Guid? PropertyGroupId { get; private set; }
    public Guid? ProjectModelId { get; private set; }

    // Status
    public string Status { get; private set; } = null!; // Draft, InProgress, Completed

    // Final Values
    public decimal? FinalAppraisedValue { get; private set; }

    // Use system pricing calculation
    public bool UseSystemCalc { get; private set; } = true;

    private PricingAnalysis()
    {
    }

    /// <summary>
    /// Creates a PricingAnalysis for a PropertyGroup.
    /// </summary>
    public static PricingAnalysis CreateForPropertyGroup(Guid propertyGroupId)
    {
        if (propertyGroupId == Guid.Empty)
            throw new ArgumentException("PropertyGroupId must not be empty.", nameof(propertyGroupId));

        return new PricingAnalysis
        {
            Id = Guid.CreateVersion7(),
            SubjectType = PricingAnalysisSubjectType.PropertyGroup,
            PropertyGroupId = propertyGroupId,
            ProjectModelId = null,
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
            PropertyGroupId = null,
            ProjectModelId = projectModelId,
            Status = "Draft"
        };
    }

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

    private void SetFinalAppraisedValueInternal(decimal? value)
    {
        FinalAppraisedValue = value;

        if (SubjectType == PricingAnalysisSubjectType.PropertyGroup && PropertyGroupId.HasValue)
        {
            // Triggers recalculation of the appraisal-level ValuationAnalysis summary.
            AddDomainEvent(new AppraisalFinalValuesChangedEvent(PropertyGroupId.Value));
        }
        else if (SubjectType == PricingAnalysisSubjectType.ProjectModel && ProjectModelId.HasValue)
        {
            // Future subscribers can use this to propagate the model's standard price downstream.
            AddDomainEvent(new ProjectModelPricingFinalValueChangedEvent(Id, ProjectModelId.Value, value));
        }
    }

    public void SetUseSystemCalc(bool value)
    {
        UseSystemCalc = value;
    }
}