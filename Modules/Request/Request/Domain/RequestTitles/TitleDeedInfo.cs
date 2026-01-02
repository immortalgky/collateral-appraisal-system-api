namespace Request.Domain.RequestTitles;

public class TitleDeedInfo : ValueObject
{
    public string? TitleNo { get; }
    public string? DeedType { get; }

    private TitleDeedInfo(string? titleNo, string? deedType)
    {
        TitleNo = titleNo;
        DeedType = deedType;
    }

    public static TitleDeedInfo Create(string? titleNo, string? deedType)
    {
        return new TitleDeedInfo(titleNo, deedType);
    }

    public TitleDeedInfo Update(string? titleNo, string? deedType)
    {
        return new TitleDeedInfo(titleNo, deedType);
    }

    private static readonly string[] ValidDeedTypes = { "DEED", "NS3", "NS3K" };

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(TitleNo);
        ArgumentException.ThrowIfNullOrWhiteSpace(DeedType);
        if (!ValidDeedTypes.Contains(DeedType)) throw new ArgumentException("deedType out of scope.");
    }
}