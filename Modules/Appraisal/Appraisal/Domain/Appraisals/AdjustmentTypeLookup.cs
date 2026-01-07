namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Standard adjustment types with typical ranges.
/// Used as a reference for comparable adjustments.
/// </summary>
public class AdjustmentTypeLookup : Entity<Guid>
{
    public string AdjustmentCategory { get; private set; } = null!;  // Location, Physical, Economic, Time, Other
    public string AdjustmentType { get; private set; } = null!;      // Specific type name
    public string Description { get; private set; } = null!;

    // Typical Range (guidance for appraisers)
    public decimal TypicalMinPercent { get; private set; }
    public decimal TypicalMaxPercent { get; private set; }

    // Display Order
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; }

    // Property Type Applicability (JSON array, null = all)
    public string? ApplicablePropertyTypes { get; private set; }  // ["Land", "Building"]

    private AdjustmentTypeLookup() { }

    public static AdjustmentTypeLookup Create(
        string adjustmentCategory,
        string adjustmentType,
        string description,
        decimal typicalMinPercent,
        decimal typicalMaxPercent,
        int displayOrder = 0)
    {
        ValidateCategory(adjustmentCategory);

        if (typicalMinPercent > typicalMaxPercent)
            throw new ArgumentException("TypicalMinPercent cannot be greater than TypicalMaxPercent");

        return new AdjustmentTypeLookup
        {
            Id = Guid.NewGuid(),
            AdjustmentCategory = adjustmentCategory,
            AdjustmentType = adjustmentType,
            Description = description,
            TypicalMinPercent = typicalMinPercent,
            TypicalMaxPercent = typicalMaxPercent,
            DisplayOrder = displayOrder,
            IsActive = true
        };
    }

    private static void ValidateCategory(string category)
    {
        var validCategories = new[] { "Location", "Physical", "Economic", "Time", "Other" };
        if (!validCategories.Contains(category))
            throw new ArgumentException($"Invalid adjustment category: {category}");
    }

    public void SetApplicablePropertyTypes(string? propertyTypesJson)
    {
        ApplicablePropertyTypes = propertyTypesJson;
    }

    public void UpdateRange(decimal minPercent, decimal maxPercent)
    {
        if (minPercent > maxPercent)
            throw new ArgumentException("MinPercent cannot be greater than MaxPercent");

        TypicalMinPercent = minPercent;
        TypicalMaxPercent = maxPercent;
    }

    public void UpdateDisplayOrder(int order)
    {
        DisplayOrder = order;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Check if an adjustment percent is within the typical range.
    /// </summary>
    public bool IsWithinTypicalRange(decimal adjustmentPercent)
    {
        return adjustmentPercent >= TypicalMinPercent && adjustmentPercent <= TypicalMaxPercent;
    }
}
