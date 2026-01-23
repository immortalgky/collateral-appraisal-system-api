namespace Request.Domain.RequestTitles;

public class LandLocationInfo : ValueObject
{
    public string? BookNumber { get; set; }
    public string? PageNumber { get; set; }
    public string? LandParcelNumber { get; set; }
    public string? SurveyNumber { get; set; }
    public string? MapSheetNumber { get; set; }
    public string? Rawang { get; set; }
    public string? AerialMapName { get; set; }
    public string? AerialMapNumber { get; set; }

    private LandLocationInfo(string? bookNumber, string? pageNumber, string? landParcelNumber,
        string? surveyNumber, string? mapSheetNumber, string? rawang, string? aerialMapName, string? aerialMapNumber)
    {
        BookNumber = bookNumber;
        PageNumber = pageNumber;
        LandParcelNumber = landParcelNumber;
        SurveyNumber = surveyNumber;
        MapSheetNumber = mapSheetNumber;
        Rawang = rawang;
        AerialMapName = aerialMapName;
        AerialMapNumber = aerialMapNumber;
    }

    public static LandLocationInfo Create(string? bookNumber, string? pageNumber, string? landParcelNumber,
        string? surveyNumber, string? mapSheetNumber, string? rawang, string? aerialMapName, string? aerialMapNumber)
    {
        return new LandLocationInfo(bookNumber, pageNumber, landParcelNumber, surveyNumber, mapSheetNumber, rawang,
            aerialMapName, aerialMapNumber);
    }

    public void Validate()
    {
        // Add validation logic if needed
    }
}