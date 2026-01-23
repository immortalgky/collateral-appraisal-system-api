namespace Request.Domain.RequestTitles;

public class TitleDeedInfo : ValueObject
{
    public string? TitleNumber { get; }
    public string? TitleType { get; }

    private TitleDeedInfo(string? titleNumber, string? titleType)
    {
        TitleNumber = titleNumber;
        TitleType = titleType;
    }

    public static TitleDeedInfo Create(string? titleNumber, string? titleType)
    {
        return new TitleDeedInfo(titleNumber, titleType);
    }

    public TitleDeedInfo Update(string? titleNumber, string? titleType)
    {
        return new TitleDeedInfo(titleNumber, titleType);
    }

    private static readonly string[] ValidDeedTypes = { "DEED", "NS3", "NS3K" };

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(TitleNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(TitleType);
        if (!ValidDeedTypes.Contains(TitleType)) throw new ArgumentException("deedType out of scope.");
    }
}