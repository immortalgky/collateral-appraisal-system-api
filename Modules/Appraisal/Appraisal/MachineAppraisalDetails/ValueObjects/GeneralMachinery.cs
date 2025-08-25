namespace Appraisal.MachineAppraisalDetails.ValueObjects;

public class GeneralMachinery : ValueObject
{
    public string? Industrial { get; }
    public int? SurveyNo { get; }
    public int? ApprNo { get; }

    private GeneralMachinery() { }

    private GeneralMachinery(
        string? industrial,
        int? surveyNo,
        int? appNo
    )
    {
        Industrial = industrial;
        SurveyNo = surveyNo;
        ApprNo = appNo;
    }

    public static GeneralMachinery Crate(
        string? industrial,
        int? surveyNo,
        int? appNo
    )
    {
        return new GeneralMachinery(industrial, surveyNo, appNo);
    }
}