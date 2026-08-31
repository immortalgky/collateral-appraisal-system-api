namespace Collateral.Application.Features.BlockReappraisal.GetBlockReappraisalDetail;

/// <summary>
/// Returns the block-project structure for a given CollateralMaster — deserialized from
/// collateral.ProjectDetails.StructureJson — along with the due-list header fields.
/// Used to render the sold/available donut chart and units table on the screen.
/// </summary>
public record GetBlockReappraisalDetailQuery(Guid CollateralMasterId)
    : IQuery<BlockReappraisalDetailResult?>;

public record BlockReappraisalDetailResult(
    Guid CollateralMasterId,
    string? OldAppraisalNumber,
    string? ProjectName,
    string ProjectType,
    decimal? ProjectSellingPrice,
    int TotalUnits,
    int RemainingUnits,
    DateTime? LastAppraisedDate,
    DateTime? DueDate,
    int SoldUnits,
    BlockReappraisalStructureDto Structure);

// ─── Snapshot DTOs (must match PascalCase produced by default JsonSerializer.Serialize) ───

public record BlockReappraisalStructureDto(
    string? ProjectType,
    string? ProjectName,
    string? Developer,
    string? Address,
    string? Province,
    decimal? Latitude,
    decimal? Longitude,
    int TotalUnits,
    int RemainingUnits,
    decimal? ProjectSellingPrice,
    List<BlockReappraisalUnitDto> Units,
    List<BlockReappraisalModelDto> Models,
    List<BlockReappraisalTowerDto> Towers);

public record BlockReappraisalUnitDto(
    int SequenceNumber,
    bool IsSold,
    string? ModelType,
    decimal? UsableArea,
    decimal? SellingPrice,
    int? Floor,
    string? TowerName,
    string? CondoRegistrationNumber,
    string? RoomNumber,
    string? PlotNumber,
    string? HouseNumber,
    int? NumberOfFloors,
    decimal? LandArea,
    decimal? LastAppraisedValue,
    DateTime? UpdatedAt,
    string? UpdatedBy
    );

public record BlockReappraisalModelDto(
    string? ModelName);

public record BlockReappraisalTowerDto(
    string? TowerName);
