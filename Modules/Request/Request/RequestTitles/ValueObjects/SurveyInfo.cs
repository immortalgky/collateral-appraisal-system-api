namespace Request.RequestTitles.ValueObjects;

public class SurveyInfo : ValueObject
{
    public string? Rawang { get; }
    public string? LandNo { get; }
    public string? SurveyNo { get; }

    private SurveyInfo(string? rawang, string? landNo, string? surveyNo)
    {
        Rawang = rawang;
        LandNo = landNo;
        SurveyNo = surveyNo;
    }

    public static SurveyInfo Create(string? rawang, string? landNo, string? surveyNo)
    {
        return new SurveyInfo(rawang, landNo, surveyNo);
    }

    public SurveyInfo Update(string? rawang, string? landNo, string? surveyNo)
    {
        return new SurveyInfo(rawang, landNo, surveyNo);
    }
}
