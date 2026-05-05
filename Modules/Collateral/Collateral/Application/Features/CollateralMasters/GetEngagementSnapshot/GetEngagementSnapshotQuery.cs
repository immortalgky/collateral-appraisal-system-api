namespace Collateral.Application.Features.CollateralMasters.GetEngagementSnapshot;

public record GetEngagementSnapshotQuery(
    Guid CollateralMasterId,
    Guid EngagementId
) : IQuery<GetEngagementSnapshotResult>;
