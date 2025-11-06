namespace Request.Requests.ValueObjects;

public class Fee : ValueObject
{
    public string? FeePaymentType { get; }
    public decimal? AbsorbedFee { get; }
    public string? FeeNotes { get; }

    private Fee(
        string? feePaymentType, 
        decimal? absorbedFee,
        string? feeNotes
    )
    {
        FeePaymentType = feePaymentType;
        AbsorbedFee = absorbedFee;
        FeeNotes = feeNotes;
    }

    public static Fee Create(
        string? feePaymentType, 
        decimal? absorbedFee,
        string? feeNotes
    )
    {
        return new Fee(feePaymentType, absorbedFee, feeNotes);
    }
}