namespace Request.Domain.RequestTitles;

public class LandLocationInfo : ValueObject
{
    public string? Rawang { get; }
    public string? LandNo { get; }
    public string? SurveyNo { get; }

    private LandLocationInfo(string? rawang, string? landNo, string? surveyNo)
    {
        Rawang = rawang;
        LandNo = landNo;
        SurveyNo = surveyNo;
    }

    public static LandLocationInfo Create(string? rawang, string? landNo, string? surveyNo)
    {
        return new LandLocationInfo(rawang, landNo, surveyNo);
    }

    public LandLocationInfo Update(string? rawang, string? landNo, string? surveyNo)
    {
        return new LandLocationInfo(rawang, landNo, surveyNo);
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Rawang);
        ArgumentException.ThrowIfNullOrWhiteSpace(LandNo);
        ArgumentException.ThrowIfNullOrWhiteSpace(SurveyNo);
    }
}
