using Workflow.Contracts.FeeAppointmentApprovals;
using Workflow.FeeAppointmentApprovals.Application.Policy;

namespace Workflow.FeeAppointmentApprovals.Application.Queries;

/// <summary>
/// Handles the read-only EvaluateFeeAppointmentApprovalQuery by delegating to the same
/// FeeAppointmentApprovalPolicyService used at raise-time, guaranteeing edit-time and
/// submit-time verdicts are produced by identical logic.
/// No state is mutated; no workflow is created.
/// </summary>
public class EvaluateFeeAppointmentApprovalQueryHandler(
    IFeeAppointmentApprovalPolicyService policyService,
    ILogger<EvaluateFeeAppointmentApprovalQueryHandler> logger)
    : IQueryHandler<EvaluateFeeAppointmentApprovalQuery, EvaluateFeeAppointmentApprovalResult>
{
    public async Task<EvaluateFeeAppointmentApprovalResult> Handle(
        EvaluateFeeAppointmentApprovalQuery query,
        CancellationToken cancellationToken)
    {
        var appointmentRequiresApproval = false;
        if (query.ProposedAppointmentDate.HasValue)
        {
            appointmentRequiresApproval = await policyService.RequiresAppointmentApprovalAsync(
                query.ProposedAppointmentDate.Value,
                query.RescheduleCount ?? 0,
                query.RequestSource,
                cancellationToken);

            logger.LogDebug(
                "Evaluated appointment approval for appraisal {AppraisalId}: date={Date}, reschedule={Count}, requires={Requires}",
                query.AppraisalId, query.ProposedAppointmentDate.Value, query.RescheduleCount ?? 0, appointmentRequiresApproval);
        }

        var feesRequireApproval = false;
        if (query.CumulativeAddedFeeTotal.HasValue && query.CumulativeAddedFeeTotal.Value > 0)
        {
            var feeTier = await policyService.GetFeeTierAsync(
                query.CumulativeAddedFeeTotal.Value,
                query.RequestSource,
                cancellationToken);

            feesRequireApproval = feeTier is not null;

            logger.LogDebug(
                "Evaluated fee approval for appraisal {AppraisalId}: cumulativeTotal={Total}, requires={Requires}",
                query.AppraisalId, query.CumulativeAddedFeeTotal.Value, feesRequireApproval);
        }

        return new EvaluateFeeAppointmentApprovalResult(appointmentRequiresApproval, feesRequireApproval);
    }
}
