namespace Collateral.Application.Features.CollateralMasters.GetBackfillReport;

public record GetBackfillReportResult(PaginatedResult<BackfillReportItemDto> Items);

public record BackfillReportItemDto(
    Guid Id,
    Guid AppraisalId,
    string Status,
    string? Message,
    DateTime RunAt);
