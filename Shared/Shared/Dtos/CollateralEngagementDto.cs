namespace Shared.Dtos;

public record CollateralEngagementDto(
    long CollatId,
    long ReqId,
    DateTime? LinkedAt,
    DateTime? UnlinkedAt,
    bool IsActive
);