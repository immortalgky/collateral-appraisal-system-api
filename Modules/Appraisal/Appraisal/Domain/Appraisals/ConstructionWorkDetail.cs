namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Individual work item detail within a construction inspection (full detail mode).
/// Owned by ConstructionInspection via OwnsMany.
/// Tracks construction value, progress %, and computed property values.
/// </summary>
public class ConstructionWorkDetail : Entity<Guid>
{
    public Guid ConstructionInspectionId { get; private set; }
    public Guid ConstructionWorkGroupId { get; private set; }
    public Guid? ConstructionWorkItemId { get; private set; }
    public string WorkItemName { get; private set; } = null!;
    public int DisplayOrder { get; private set; }

    // User-entered values
    public decimal ProportionPct { get; private set; }
    public decimal PreviousProgressPct { get; private set; }
    public decimal CurrentProgressPct { get; private set; }

    // Server-computed values
    public decimal ConstructionValue { get; private set; }
    public decimal CurrentProportionPct { get; private set; }
    public decimal PreviousPropertyValue { get; private set; }
    public decimal CurrentPropertyValue { get; private set; }

    private ConstructionWorkDetail()
    {
        // For EF Core
    }

    public static ConstructionWorkDetail Create(
        Guid constructionInspectionId,
        Guid constructionWorkGroupId,
        string workItemName,
        int displayOrder,
        decimal proportionPct,
        decimal previousProgressPct,
        decimal currentProgressPct,
        Guid? constructionWorkItemId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workItemName);

        return new ConstructionWorkDetail
        {
            ConstructionInspectionId = constructionInspectionId,
            ConstructionWorkGroupId = constructionWorkGroupId,
            ConstructionWorkItemId = constructionWorkItemId,
            WorkItemName = workItemName,
            DisplayOrder = displayOrder,
            ProportionPct = proportionPct,
            PreviousProgressPct = previousProgressPct,
            CurrentProgressPct = currentProgressPct
        };
    }

    /// <summary>
    /// Compute derived values based on total construction value.
    ///
    /// Each money figure is rounded to whole baht as it is produced, and the next one is derived
    /// from the already-rounded ConstructionValue rather than from the raw product — see
    /// <see cref="ConstructionMoney"/>. The construction-inspection screen recomputes these same
    /// figures client-side while the inspector types, and mirrors this chain step for step, so
    /// what is on screen matches what is stored. CurrentProportionPct is a percentage, not money,
    /// and keeps its fractional part.
    /// </summary>
    public void ComputeValues(decimal totalValue)
    {
        ConstructionValue = ConstructionMoney.ToBaht(totalValue * (ProportionPct / 100));
        CurrentProportionPct = ProportionPct * (CurrentProgressPct / 100);
        PreviousPropertyValue = ConstructionMoney.ToBaht(ConstructionValue * (PreviousProgressPct / 100));
        CurrentPropertyValue = ConstructionMoney.ToBaht(ConstructionValue * (CurrentProgressPct / 100));
    }

    public void Update(
        Guid constructionWorkGroupId,
        string workItemName,
        int displayOrder,
        decimal proportionPct,
        decimal previousProgressPct,
        decimal currentProgressPct,
        Guid? constructionWorkItemId = null)
    {
        ConstructionWorkGroupId = constructionWorkGroupId;
        ConstructionWorkItemId = constructionWorkItemId;
        WorkItemName = workItemName;
        DisplayOrder = displayOrder;
        ProportionPct = proportionPct;
        PreviousProgressPct = previousProgressPct;
        CurrentProgressPct = currentProgressPct;
    }
}