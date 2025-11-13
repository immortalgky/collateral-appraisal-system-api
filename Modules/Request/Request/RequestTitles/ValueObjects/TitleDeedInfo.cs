namespace Request.RequestTitles.ValueObjects;

public class TitleDeedInfo : ValueObject
{
    public string? TitleNo { get; }
    public string? DeedType { get; }
    public string? TitleDetail { get; }

    private TitleDeedInfo( string? titleNo, string? deedType, string? titleDetail )
    {
        TitleNo = titleNo;
        DeedType = deedType;
        TitleDetail = titleDetail;
    }

    public static TitleDeedInfo Create(string? titleNo, string? deedType, string? titleDetail)
    {
        return new TitleDeedInfo(titleNo, deedType, titleDetail) ;
    }

    public TitleDeedInfo Update( string? titleNo, string? deedType, string? titleDetail )
    {
        return new TitleDeedInfo(titleNo, deedType, titleDetail) ;
    }
}
