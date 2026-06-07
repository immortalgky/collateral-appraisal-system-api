namespace Collateral.Application.Features.BlockUnitMaintenance.GetBlockUnitMaintenanceUnits;

/// <summary>
/// Read-only projection of a collateral.ProjectUnit for the Block Unit Maintenance screen.
/// Includes the three sale-tracking fields plus enough context for the inline table.
/// </summary>
public record BlockUnitMaintenanceUnitDto(
    Guid Id,
    int SequenceNumber,
    string? ModelType,
    decimal? UsableArea,
    decimal? SellingPrice,
    // Condo
    int? Floor,
    string? TowerName,
    string? CondoRegistrationNumber,
    string? RoomNumber,
    // LandAndBuilding
    string? PlotNumber,
    string? HouseNumber,
    int? NumberOfFloors,
    decimal? LandArea,
    // Sale tracking
    bool IsSold,
    // "Cash" | "Loan" | null — enum name string (stored as nvarchar(10) in collateral.ProjectUnits)
    string? PurchaseBy,
    string? LoanBankName
);

/// <summary>
/// Project header info shown in the detail-page hero, sourced from collateral.ProjectDetails.
/// </summary>
public record BlockUnitMaintenanceProjectDto(
    Guid CollateralMasterId,
    string? AppraisalReportNo,
    string? ProjectName,
    string ProjectType
);

/// <summary>
/// Detail-page payload: project header + its units.
/// </summary>
public record BlockUnitMaintenanceDetailDto(
    BlockUnitMaintenanceProjectDto Project,
    IReadOnlyList<BlockUnitMaintenanceUnitDto> Units
);
