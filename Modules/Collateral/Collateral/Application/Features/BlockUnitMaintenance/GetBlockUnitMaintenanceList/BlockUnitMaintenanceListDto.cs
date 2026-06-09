namespace Collateral.Application.Features.BlockUnitMaintenance.GetBlockUnitMaintenanceList;

public record BlockUnitMaintenanceListDto(
    Guid CollateralMasterId,
    string? AppraisalReportNo,
    string? CustomerName,
    string? ProjectName,
    string ProjectType,
    string? Developer,
    int TotalUnits,
    int SoldUnits,
    int UnsoldUnits,
    DateTime? UpdatedOn,
    string? UpdatedBy
);
