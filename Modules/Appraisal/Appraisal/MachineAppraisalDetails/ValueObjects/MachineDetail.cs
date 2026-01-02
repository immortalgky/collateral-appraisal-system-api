namespace Appraisal.MachineAppraisalDetails.ValueObjects;

public class MachineDetail : ValueObject
{
    public GeneralMachinery? GeneralMachinery { get; }
    public AtSurveyDate? AtSurveyDate { get; }
    public RightsAndConditionsOfLegalRestrictions? RightsAndConditionsOfLegalRestrictions { get; }

    private MachineDetail() { }
    private MachineDetail(
        GeneralMachinery? generalMachinery,
        AtSurveyDate? atSurveyDate,
        RightsAndConditionsOfLegalRestrictions? rightsAndConditionsOfLegalRestrictions

    )
    {
        GeneralMachinery = generalMachinery;
        AtSurveyDate = atSurveyDate;
        RightsAndConditionsOfLegalRestrictions = rightsAndConditionsOfLegalRestrictions;
    }

    public static MachineDetail Create(
        GeneralMachinery? generalMachinery,
        AtSurveyDate? atSurveyDate,
        RightsAndConditionsOfLegalRestrictions? rightsAndConditionsOfLegalRestrictions
    )
    {
        return new MachineDetail(generalMachinery, atSurveyDate, rightsAndConditionsOfLegalRestrictions);
    }
}