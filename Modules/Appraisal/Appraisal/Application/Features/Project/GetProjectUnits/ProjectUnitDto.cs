namespace Appraisal.Application.Features.Project.GetProjectUnits;

/// <summary>
/// DTO for a project unit.
/// Condo-side fields (Floor, TowerName, CondoRegistrationNumber, RoomNumber) will be null for LB units.
/// LB-side fields (PlotNumber, HouseNumber, NumberOfFloors, LandArea) will be null for Condo units.
/// </summary>
public record ProjectUnitDto(
    Guid Id,
    Guid ProjectId,
    Guid UploadBatchId,
    int SequenceNumber,
    // Common
    string? ModelType,
    decimal? UsableArea,
    decimal? SellingPrice,
    // Condo-side
    int? Floor,
    string? TowerName,
    string? CondoRegistrationNumber,
    string? RoomNumber,
    // LB-side
    string? PlotNumber,
    string? HouseNumber,
    int? NumberOfFloors,
    decimal? LandArea
);
