namespace Appraisal.Application.Features.BlockCondo.GetCondoUnits;

public record GetCondoUnitsResult(
    IReadOnlyList<CondoUnitDto> Units,
    IReadOnlyList<string> Towers,
    IReadOnlyList<string> Models,
    int TotalUnits
);

public record CondoUnitDto(
    Guid Id,
    Guid AppraisalId,
    Guid UploadBatchId,
    int SequenceNumber,
    int? Floor,
    string? TowerName,
    string? CondoRegistrationNumber,
    string? RoomNumber,
    string? ModelType,
    decimal? UsableArea,
    decimal? SellingPrice
);
