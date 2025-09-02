namespace Collateral.CollateralProperties.ValueObjects;

public class LandTitleDocumentDetail : ValueObject
{
    public string TitleNo { get; }
    public string BookNo { get; }
    public string PageNo { get; }
    public string LandNo { get; }
    public string SurveyNo { get; }
    public string? SheetNo { get; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    private LandTitleDocumentDetail(
        string titleNo,
        string bookNo,
        string pageNo,
        string landNo,
        string surveyNo,
        string? sheetNo
    )
    {
        TitleNo = titleNo;
        BookNo = bookNo;
        PageNo = pageNo;
        LandNo = landNo;
        SurveyNo = surveyNo;
        SheetNo = sheetNo;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static LandTitleDocumentDetail Create(
        string titleNo,
        string bookNo,
        string pageNo,
        string landNo,
        string surveyNo,
        string? sheetNo
    )
    {
        return new LandTitleDocumentDetail(titleNo, bookNo, pageNo, landNo, surveyNo, sheetNo);
    }
}
