namespace Appraisal.Application.Features.BlockUnitMaintenance.GetBlockUnitMaintenanceList;

public record BlockUnitMaintenanceListDto(
    Guid ProjectId,
    Guid AppraisalId,
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
