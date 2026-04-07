namespace Appraisal.Application.Features.BlockVillage.SaveVillageProjectLand;

public record VillageProjectLandTitleDto(
    Guid? Id,
    string TitleNumber,
    string TitleType,
    string? BookNumber = null,
    string? PageNumber = null,
    string? LandParcelNumber = null,
    string? SurveyNumber = null,
    string? MapSheetNumber = null,
    string? Rawang = null,
    string? AerialMapName = null,
    string? AerialMapNumber = null,
    decimal? Rai = null,
    decimal? Ngan = null,
    decimal? SquareWa = null,
    string? BoundaryMarkerType = null,
    string? BoundaryMarkerRemark = null,
    string? DocumentValidationResultType = null,
    bool? IsMissingFromSurvey = null,
    decimal? GovernmentPricePerSqWa = null,
    decimal? GovernmentPrice = null,
    string? Remark = null
);
