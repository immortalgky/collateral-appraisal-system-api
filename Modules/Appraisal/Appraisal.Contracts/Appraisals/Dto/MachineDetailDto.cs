namespace Appraisal.Contracts.Appraisals.Dto;

public record MachineDetailDto(
    GeneralMachineryDto GeneralMachinery,
    AtSurveyDateDto AtSurveyDate,
    RightsAndConditionsOfLegalRestrictionsDto RightsAndConditionsOfLegalRestrictions
);
