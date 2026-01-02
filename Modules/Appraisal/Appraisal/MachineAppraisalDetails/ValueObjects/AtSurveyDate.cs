namespace Appraisal.MachineAppraisalDetails.ValueObjects;

public class AtSurveyDate : ValueObject
{
    public int? Installed { get; }
    public string? ApprScrap { get; }
    public int? NoOfAppraise { get; }
    public int? NotInstalled { get; }
    public string? Maintenance { get; }
    public string? Exterior { get; }
    public string? Performance { get; }
    public bool? MarketDemand { get; }
    public string? MarketDemandRemark { get; }

    private AtSurveyDate() { }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    private AtSurveyDate(
            int installed,
            string apprScrap,
            int noOfAppraise,
            int notInstalled,
            string maintenance,
            string exterior,
            string performance,
            bool marketDemand,
            string marketDemandRemark
        )
    {
        Installed = installed;
        ApprScrap = apprScrap;
        NoOfAppraise = noOfAppraise;
        NotInstalled = notInstalled;
        Maintenance = maintenance;
        Exterior = exterior;
        Performance = performance;
        MarketDemand = marketDemand;
        MarketDemandRemark = marketDemandRemark;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static AtSurveyDate Create(
        int installed,
        string apprScrap,
        int noOfAppraise,
        int notInstalled,
        string maintenance,
        string exterior,
        string performance,
        bool marketDemand,
        string marketDemandRemark
    )
    {
        return new AtSurveyDate(
            installed,
            apprScrap,
            noOfAppraise,
            notInstalled,
            maintenance,
            exterior,
            performance,
            marketDemand,
            marketDemandRemark
        );
    }
}