using Shared.Messaging.Events;
using Workflow.Contracts.FeeAppointmentApprovals;

namespace Workflow.FeeAppointmentApprovals.EventHandlers;

/// <summary>
/// Consumes FeeAppointmentApprovalRequestedIntegrationEvent published by the Appraisal module
/// and dispatches RaiseFeeAppointmentApprovalCommand to create the FeeAppointmentApproval aggregate
/// and spawn the child approval workflow.
/// </summary>
public class FeeAppointmentApprovalRequestedConsumer(
    ISender sender,
    ILogger<FeeAppointmentApprovalRequestedConsumer> logger)
    : IConsumer<FeeAppointmentApprovalRequestedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<FeeAppointmentApprovalRequestedIntegrationEvent> context)
    {
        var msg = context.Message;

        logger.LogInformation(
            "Consuming FeeAppointmentApprovalRequested for appraisal {AppraisalId}, {LineCount} lines",
            msg.AppraisalId, msg.Lines.Count);

        var lines = msg.Lines
            .Select(l => new RaiseFeeApprovalLineDto(
                l.LineType,
                l.TargetId,
                l.NewDate,
                l.RescheduleCount,
                l.FeeCode,
                l.FeeDescription,
                l.FeeAmount))
            .ToList();

        var command = new RaiseFeeAppointmentApprovalCommand(
            msg.AppraisalId,
            msg.RequestSource,
            lines);

        var result = await sender.Send(command, context.CancellationToken);

        logger.LogInformation(
            "FeeAppointmentApproval raised: {ApprovalId} with workflow instance {InstanceId}",
            result.ApprovalId, result.FollowupWorkflowInstanceId);
    }
}
