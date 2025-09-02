namespace Appraisal.Contracts.Appraisals.Dto;

public record LandLocationDetailDto(
    string? LandLocation,
    string? LandCheck,
    string? LandCheckOther,
    string Street,
    string? Soi,
    decimal? Distance,
    string? Village,
    string? AddressLocation,
    string? LandShape,
    string? UrbanPlanningType,
    string? Location,
    string? PlotLocation,
    string? PlotLocationOther
);
