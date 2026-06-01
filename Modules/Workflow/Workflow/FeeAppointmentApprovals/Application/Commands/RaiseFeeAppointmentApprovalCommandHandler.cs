using Workflow.Contracts.FeeAppointmentApprovals;
using Workflow.FeeAppointmentApprovals.Application.Policy;
using Workflow.FeeAppointmentApprovals.Domain;
using Workflow.FeeAppointmentApprovals.Infrastructure;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Services;

namespace Workflow.FeeAppointmentApprovals.Application.Commands;

public class RaiseFeeAppointmentApprovalCommandHandler(
    WorkflowDbContext dbContext,
    IFeeAppointmentApprovalPolicyService policyService,
    IWorkflowDefinitionRepository definitionRepository,
    IWorkflowService workflowService,
    ILogger<RaiseFeeAppointmentApprovalCommandHandler> logger
) : ICommandHandler<RaiseFeeAppointmentApprovalCommand, RaiseFeeAppointmentApprovalResult>
{
    public const string WorkflowName = "Fee Appointment Approval Workflow";

    public async Task<RaiseFeeAppointmentApprovalResult> Handle(
        RaiseFeeAppointmentApprovalCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Lines is null || command.Lines.Count == 0)
            throw new ArgumentException("At least one line is required");

        // 1. Evaluate policy and determine the strictest approver tier
        var appointmentLine = command.Lines.FirstOrDefault(l => l.LineType == "Appointment");
        var feeLines = command.Lines.Where(l => l.LineType == "Fee").ToList();

        FeeApprovalTierMatch? appointmentTier = null;
        if (appointmentLine is not null && appointmentLine.NewDate.HasValue)
        {
            var requiresApproval = await policyService.RequiresAppointmentApprovalAsync(
                appointmentLine.NewDate.Value,
                appointmentLine.RescheduleCount ?? 0,
                command.RequestSource,
                cancellationToken);
            if (requiresApproval)
            {
                // Appointment-only approver: use the lowest-priority (most permissive) active tier.
                // This is typically IntAdmin — it is NOT derived by probing the fee matrix.
                appointmentTier = await policyService.GetLowestActiveTierAsync(command.RequestSource, cancellationToken);
            }
        }

        FeeApprovalTierMatch? feeTier = null;
        if (feeLines.Count > 0)
        {
            var totalFeeAmount = feeLines.Sum(l => l.FeeAmount ?? 0m);
            feeTier = await policyService.GetFeeTierAsync(totalFeeAmount, command.RequestSource, cancellationToken);
        }

        var strictestTier = policyService.PickStrictestTier(appointmentTier, feeTier);
        if (strictestTier is null)
            throw new InvalidOperationException(
                "RaiseFeeAppointmentApproval called but no tier matched — caller should not have sent requiring lines.");

        // 2. Resolve workflow definition
        var definition = await definitionRepository.GetLatestVersion(WorkflowName, cancellationToken)
                         ?? throw new InvalidOperationException(
                             $"Workflow definition '{WorkflowName}' not found. Did the seeder run?");

        // 3. Build domain lines
        var domainLines = new List<FeeAppointmentApprovalLine>();
        foreach (var dto in command.Lines)
        {
            if (dto.LineType == "Appointment")
            {
                if (!dto.NewDate.HasValue)
                    throw new ArgumentException("Appointment line must have NewDate");
                domainLines.Add(FeeAppointmentApprovalLine.CreateAppointment(
                    dto.TargetId, dto.NewDate.Value, dto.RescheduleCount ?? 0));
            }
            else if (dto.LineType == "Fee")
            {
                if (string.IsNullOrWhiteSpace(dto.FeeCode))
                    throw new ArgumentException("Fee line must have FeeCode");
                domainLines.Add(FeeAppointmentApprovalLine.CreateFee(
                    dto.TargetId,
                    dto.FeeCode!,
                    dto.FeeDescription ?? string.Empty,
                    dto.FeeAmount ?? 0m));
            }
        }

        // 4. Create aggregate
        var approval = FeeAppointmentApproval.Raise(
            command.AppraisalId,
            command.RequestSource,
            strictestTier.ApproverCode,
            strictestTier.AssignedType,
            strictestTier.TierLabel,
            domainLines);

        dbContext.FeeAppointmentApprovals.Add(approval);
        await dbContext.SaveChangesAsync(cancellationToken);

        // 5. Spawn the child workflow
        var initialVariables = new Dictionary<string, object>
        {
            ["feeAppointmentApprovalId"] = approval.Id,
            ["appraisalId"] = command.AppraisalId,
            ["approverAssignee"] = strictestTier.ApproverCode,
            ["assignedType"] = strictestTier.AssignedType
        };

        var childInstance = await workflowService.StartWorkflowAsync(
            definition.Id,
            $"FeeApptApproval-{approval.Id}",
            startedBy: strictestTier.ApproverCode,
            initialVariables: initialVariables,
            correlationId: approval.Id.ToString(),
            cancellationToken: cancellationToken);

        approval.AttachFollowupWorkflowInstance(childInstance.Id);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Raised FeeAppointmentApproval {ApprovalId} for appraisal {AppraisalId}, tier {Tier}, workflow {InstanceId}",
            approval.Id, command.AppraisalId, strictestTier.TierLabel, childInstance.Id);

        return new RaiseFeeAppointmentApprovalResult(approval.Id, childInstance.Id);
    }
}
