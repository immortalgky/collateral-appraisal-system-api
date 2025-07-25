namespace Request.Requests.ValueObjects;

public class Fee : ValueObject
{
    public string FeeType { get; }
    public string? FeeRemark { get; }

    private Fee(string feeType, string? feeRemark)
    {
        FeeType = feeType;
        FeeRemark = feeRemark;
    }

    public static Fee Create(string feeType, string? feeRemark)
    {
        return new Fee(feeType, feeRemark);
    }
}