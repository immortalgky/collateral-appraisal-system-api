namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Individual adjustment line item for a comparable.
/// </summary>
public class ComparableAdjustment : Entity<Guid>
{
    public Guid AppraisalComparableId { get; private set; }

    // Adjustment Details
    public string AdjustmentCategory { get; private set; } = null!; // Location, Physical, Economic, Time, Other
    public string AdjustmentType { get; private set; } = null!; // From lookup table
    public decimal AdjustmentPercent { get; private set; }
    public string AdjustmentDirection { get; private set; } = null!; // Positive, Negative

    // Subject vs Comparable Values (for audit)
    public string? SubjectValue { get; private set; }
    public string? ComparableValue { get; private set; }

    // Required for audit
    public string? Justification { get; private set; }

    private ComparableAdjustment()
    {
    }

    public static ComparableAdjustment Create(
        Guid appraisalComparableId,
        string adjustmentCategory,
        string adjustmentType,
        decimal adjustmentPercent,
        string? justification = null)
    {
        ValidateCategory(adjustmentCategory);

        return new ComparableAdjustment
        {
            Id = Guid.NewGuid(),
            AppraisalComparableId = appraisalComparableId,
            AdjustmentCategory = adjustmentCategory,
            AdjustmentType = adjustmentType,
            AdjustmentPercent = adjustmentPercent,
            AdjustmentDirection = adjustmentPercent >= 0 ? "Positive" : "Negative",
            Justification = justification
        };
    }

    private static void ValidateCategory(string category)
    {
        var validCategories = new[] { "Location", "Physical", "Economic", "Time", "Other" };
        if (!validCategories.Contains(category))
            throw new ArgumentException($"Invalid adjustment category: {category}");
    }

    public void SetComparison(string? subjectValue, string? comparableValue)
    {
        SubjectValue = subjectValue;
        ComparableValue = comparableValue;
    }
}