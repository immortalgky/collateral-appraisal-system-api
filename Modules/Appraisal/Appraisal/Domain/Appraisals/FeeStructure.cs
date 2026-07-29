namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Master fee configuration table. Defines available fee types and their base amounts.
/// FeeCode "01" (Appraisal Fee) has multiple rows for selling-price tiers. The human-readable
/// name is NOT stored here — it is resolved from the <c>TypeOfFee</c> parameter group by code.
///
/// Tiers are optionally scoped to a single <see cref="AppraisalType"/>; a null scope is the
/// generic ladder that applies to any appraisal type.
/// </summary>
public class FeeStructure : Entity<Guid>
{
    public string FeeCode { get; private set; } = null!;

    /// <summary>
    /// Appraisal type this tier applies to (values from <see cref="AppraisalTypes"/>), or null
    /// for the generic ladder that applies to any type. A type-scoped ladder replaces the generic
    /// one for that type rather than merging with it — see AssignmentFeeService.
    /// </summary>
    public string? AppraisalType { get; private set; }

    public decimal BaseAmount { get; private set; }
    public decimal MinSellingPrice { get; private set; }
    public decimal? MaxSellingPrice { get; private set; }
    public bool IsActive { get; private set; } = true;

    private FeeStructure()
    {
    }

    public static FeeStructure Create(
        string feeCode,
        decimal baseAmount,
        decimal minSellingPrice = 0,
        decimal? maxSellingPrice = null,
        bool isActive = true,
        string? appraisalType = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feeCode);

        return new FeeStructure
        {
            Id = Guid.CreateVersion7(),
            FeeCode = feeCode,
            AppraisalType = string.IsNullOrWhiteSpace(appraisalType) ? null : appraisalType,
            BaseAmount = baseAmount,
            MinSellingPrice = minSellingPrice,
            MaxSellingPrice = maxSellingPrice,
            IsActive = isActive
        };
    }

    /// <summary>
    /// Updates the mutable fields of the fee structure. FeeCode and AppraisalType are immutable —
    /// together they identify which ladder the tier belongs to and must not change in place.
    /// </summary>
    public void Update(
        decimal baseAmount,
        decimal minSellingPrice,
        decimal? maxSellingPrice,
        bool isActive)
    {
        BaseAmount = baseAmount;
        MinSellingPrice = minSellingPrice;
        MaxSellingPrice = maxSellingPrice;
        IsActive = isActive;
    }

    /// <summary>
    /// Returns true if the given selling price falls within this tier's range.
    /// </summary>
    public bool IsApplicableFor(decimal sellingPrice)
        => sellingPrice >= MinSellingPrice
           && (MaxSellingPrice is null || sellingPrice <= MaxSellingPrice.Value);
}
