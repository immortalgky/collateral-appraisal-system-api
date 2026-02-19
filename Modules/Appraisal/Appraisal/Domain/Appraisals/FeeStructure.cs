namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Master fee configuration table. Defines available fee types and their base amounts.
/// FeeCode "01" (Appraisal Fee) has multiple rows for selling-price tiers.
/// </summary>
public class FeeStructure : Entity<Guid>
{
    public string FeeCode { get; private set; } = null!;
    public string FeeName { get; private set; } = null!;
    public decimal BaseAmount { get; private set; }
    public decimal MinSellingPrice { get; private set; }
    public decimal? MaxSellingPrice { get; private set; }
    public bool IsActive { get; private set; } = true;

    private FeeStructure()
    {
    }

    public static FeeStructure Create(
        string feeCode,
        string feeName,
        decimal baseAmount,
        decimal minSellingPrice = 0,
        decimal? maxSellingPrice = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feeCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(feeName);

        return new FeeStructure
        {
            Id = Guid.CreateVersion7(),
            FeeCode = feeCode,
            FeeName = feeName,
            BaseAmount = baseAmount,
            MinSellingPrice = minSellingPrice,
            MaxSellingPrice = maxSellingPrice,
            IsActive = true
        };
    }

    /// <summary>
    /// Returns true if the given selling price falls within this tier's range.
    /// </summary>
    public bool IsApplicableFor(decimal sellingPrice)
        => sellingPrice >= MinSellingPrice
           && (MaxSellingPrice is null || sellingPrice <= MaxSellingPrice.Value);
}
