namespace Request.Domain.Requests;

public class Fee : ValueObject
{
    public string? FeePaymentType { get; }
    public decimal? AbsorbedAmount { get; }
    public string? FeeNotes { get; }

    private Fee(string? feePaymentType, string? feeNotes, decimal? absorbedAmount)
    {
        FeePaymentType = feePaymentType;
        FeeNotes = feeNotes;
        AbsorbedAmount = absorbedAmount;
    }

    public static Fee Create(string? feePaymentType, string? feeNotes, decimal? absorbedAmount)
    {
        return new Fee(feePaymentType, feeNotes, absorbedAmount);
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(FeePaymentType);
    }
}