namespace Request.Requests.ValueObjects;

public class Fee : ValueObject
{
    public string FeeType { get; }
    public string? FeeRemark { get; }
    public decimal? BankAbsorbAmount { get; }

    private Fee(
        string feeType, 
        string? feeRemark,
        decimal? bankAbsorbAmount
    )
    {
        FeeType = feeType;
        FeeRemark = feeRemark;
        BankAbsorbAmount = bankAbsorbAmount;
    }

    public static Fee Create(
        string feeType, 
        string? feeRemark,
        decimal? bankAbsorbAmount
    )
    {
        return new Fee(feeType, feeRemark, bankAbsorbAmount);
    }
}