namespace Request.Contracts.Requests.Dtos;

public record LandLocationInfoDto(
    string? BookNumber,
    string? PageNumber,
    string? LandParcelNumber,
    string? SurveyNumber,
    string? MapSheetNumber,
    string? Rawang,
    string? AerialMapName,
    string? AerialMapNumber
);