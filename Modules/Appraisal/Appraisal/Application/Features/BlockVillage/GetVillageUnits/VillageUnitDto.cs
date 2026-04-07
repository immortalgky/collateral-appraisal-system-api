namespace Appraisal.Application.Features.BlockVillage.GetVillageUnits;

public record VillageUnitDto(
    Guid Id,
    Guid AppraisalId,
    Guid UploadBatchId,
    int SequenceNumber,
    string? PlotNumber,
    string? HouseNumber,
    string? ModelName,
    int? NumberOfFloors,
    decimal? LandArea,
    decimal? UsableArea,
    decimal? SellingPrice
);
