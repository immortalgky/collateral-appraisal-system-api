using Workflow.FeeAppointmentApprovals.Domain;

namespace Workflow.FeeAppointmentApprovals.Application.Queries;

public record GetFeeAppointmentApprovalByWorkflowInstanceQuery(Guid WorkflowInstanceId)
    : IQuery<FeeAppointmentApprovalDto?>;

public class GetFeeAppointmentApprovalByWorkflowInstanceQueryHandler(WorkflowDbContext dbContext)
    : IQueryHandler<GetFeeAppointmentApprovalByWorkflowInstanceQuery, FeeAppointmentApprovalDto?>
{
    public async Task<FeeAppointmentApprovalDto?> Handle(
        GetFeeAppointmentApprovalByWorkflowInstanceQuery request,
        CancellationToken cancellationToken)
    {
        var approval = await dbContext.FeeAppointmentApprovals
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.FollowupWorkflowInstanceId == request.WorkflowInstanceId, cancellationToken);

        if (approval is null) return null;

        return MapToDto(approval);
    }

    private static FeeAppointmentApprovalDto MapToDto(FeeAppointmentApproval a) =>
        new(
            a.Id,
            a.AppraisalId,
            a.RequestSource,
            a.Status.ToString(),
            a.ResolvedTier,
            a.ApproverAssignee,
            a.AssignedType,
            a.FollowupWorkflowInstanceId,
            a.RaisedAt,
            a.ResolvedAt,
            a.Lines.Select(l => new FeeApprovalLineDto(
                l.Id,
                l.LineType.ToString(),
                l.TargetId,
                l.NewDate,
                l.RescheduleCount,
                l.FeeCode,
                l.FeeDescription,
                l.FeeAmount,
                l.LineStatus.ToString(),
                l.DecisionReason)).ToList());
}
