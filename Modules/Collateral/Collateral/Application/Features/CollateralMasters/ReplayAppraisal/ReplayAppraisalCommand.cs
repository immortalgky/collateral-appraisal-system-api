namespace Collateral.Application.Features.CollateralMasters.ReplayAppraisal;

public record ReplayAppraisalCommand(Guid AppraisalId) : ICommand<ReplayAppraisalResult>;

public record ReplayAppraisalResult(
    Guid AppraisalId,
    string Status,
    string? Message);
