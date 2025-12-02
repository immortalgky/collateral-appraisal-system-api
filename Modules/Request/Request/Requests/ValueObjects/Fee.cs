namespace Request.Requests.ValueObjects;

public class Fee : ValueObject
{
    public string? FeeType { get; }
    public string? FeeNotes { get; }
    public decimal? BankAbsorbAmt { get; }

    private Fee(string feeType, string? feeNote, decimal? bankAbsorbAmt)
    {
        FeeType = feeType;
        FeeNotes = feeNote;
        BankAbsorbAmt = bankAbsorbAmt;
    }

    private Fee()
    {
        //EF Core
    }

    public static Fee Create(string feeType, string? feeNote, decimal? bankAbsorbAmt)
    {
        return new Fee(feeType, feeNote, bankAbsorbAmt);
    }
}