namespace Appraisal.Application.Features.Project.GetProjectLand;

/// <summary>DTO for a project land title in query responses.</summary>
public record ProjectLandTitleResultDto(
    Guid Id,
    string TitleNumber,
    string TitleType,
    string? BookNumber,
    string? PageNumber,
    string? LandParcelNumber,
    string? SurveyNumber,
    string? MapSheetNumber,
    string? Rawang,
    string? AerialMapName,
    string? AerialMapNumber,
    decimal? Rai,
    decimal? Ngan,
    decimal? SquareWa,
    string? BoundaryMarkerType,
    string? BoundaryMarkerRemark,
    string? DocumentValidationResultType,
    bool? IsMissingFromSurvey,
    decimal? GovernmentPricePerSqWa,
    decimal? GovernmentPrice,
    string? Remark
);
