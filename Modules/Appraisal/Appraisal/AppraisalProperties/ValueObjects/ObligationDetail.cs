namespace Appraisal.AppraisalProperties.ValueObjects;

public class ObligationDetail : ValueObject
{
    public string IsObligation { get; }
    public string? Obligation { get; }

    private ObligationDetail(string isObligation, string? obligation)
    {
        IsObligation = isObligation;
        Obligation = obligation;
    }

    public static ObligationDetail Create(string isObligation, string? obligation)
    {
        return new ObligationDetail(isObligation, obligation);
    }
}
