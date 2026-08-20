namespace Request.Domain.RequestTitles;

public class TitleDeedInfo : ValueObject
{
    public string? TitleNumber { get; }
    public string? TitleType { get; }
    public string? BuiltOnTitleNumber { get; }

    private TitleDeedInfo(string? titleNumber, string? titleType, string? builtOnTitleNumber)
    {
        TitleNumber = titleNumber;
        TitleType = titleType;
        BuiltOnTitleNumber = builtOnTitleNumber;
    }

    public static TitleDeedInfo Create(string? titleNumber, string? titleType, string? builtOnTitleNumber)
    {
        return new TitleDeedInfo(titleNumber, titleType, builtOnTitleNumber);
    }

    public TitleDeedInfo Update(string? titleNumber, string? titleType, string? builtOnTitleNumber)
    {
        return new TitleDeedInfo(titleNumber, titleType, builtOnTitleNumber);
    }

    private static readonly string[] ValidDeedTypes = { "DEED", "NS3", "NS3K", "NS3KO", "POSR", "OTHER" };
    private static readonly string[] CondoType = { "08", "28", "33"};

    public void Validate(string? collateralType = default)
    {
        if (!CondoType.Contains(collateralType))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(TitleNumber);
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(TitleType);
        if (!ValidDeedTypes.Contains(TitleType)) throw new ArgumentException("deedType out of scope.");
    }
}