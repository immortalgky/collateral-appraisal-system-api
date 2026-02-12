namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Master fee configuration table. Defines available fee types and their base amounts.
/// </summary>
public class FeeStructure : Entity<Guid>
{
    public string FeeCode { get; private set; } = null!;
    public string FeeName { get; private set; } = null!;
    public decimal BaseAmount { get; private set; }
    public bool IsActive { get; private set; } = true;

    private FeeStructure()
    {
    }

    public static FeeStructure Create(string feeCode, string feeName, decimal baseAmount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feeCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(feeName);

        return new FeeStructure
        {
            Id = Guid.CreateVersion7(),
            FeeCode = feeCode,
            FeeName = feeName,
            BaseAmount = baseAmount,
            IsActive = true
        };
    }
}
