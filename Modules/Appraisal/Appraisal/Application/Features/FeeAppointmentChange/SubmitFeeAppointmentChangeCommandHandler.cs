using Shared.Data.Outbox;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.FeeAppointmentChange;

/// <summary>
/// Handles the combined external-company fee+appointment change request.
///
/// Security: verifies the submitting company owns the named assignment before mutating state.
///
/// Flow:
/// 1. Load appraisal + verify the named assignment belongs to the requesting company (IDOR gate).
/// 2. Apply domain mutations (Reschedule / fee.AddItem with requiresApproval=true).
/// 3. For all change lines: publish FeeAppointmentApprovalRequestedIntegrationEvent via outbox
///    → Workflow module evaluates policy and either auto-approves or creates a FeeAppointmentApproval.
/// </summary>
public class SubmitFeeAppointmentChangeCommandHandler(
    IAppraisalRepository appraisalRepository,
    AppraisalDbContext dbContext,
    IIntegrationEventOutbox outbox,
    ILogger<SubmitFeeAppointmentChangeCommandHandler> logger
) : ICommandHandler<SubmitFeeAppointmentChangeCommand, Unit>
{
    public async Task<Unit> Handle(
        SubmitFeeAppointmentChangeCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(command.AppraisalId, cancellationToken)
                        ?? throw new NotFoundException("Appraisal", command.AppraisalId);

        // ─── IDOR gate: verify the requesting company owns the named assignment ───
        var assignment = appraisal.Assignments
            .FirstOrDefault(a => a.Id == command.AssignmentId);

        if (assignment is null)
            throw new InvalidOperationException(
                $"Assignment {command.AssignmentId} not found on appraisal {command.AppraisalId}");

        // AssigneeCompanyId is stored as a string (Guid-as-string per the appraisal pattern).
        // RequestedByCompanyId is the company_id claim (also a Guid-as-string).
        if (!string.Equals(assignment.AssigneeCompanyId, command.RequestedByCompanyId,
                StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Company {RequestedByCompanyId} attempted to submit fee/appointment change for assignment {AssignmentId} " +
                "owned by company {AssigneeCompanyId}. Request rejected.",
                command.RequestedByCompanyId, command.AssignmentId, assignment.AssigneeCompanyId);
            throw new UnauthorizedAccessException(
                "Your company does not own this assignment");
        }

        var requiresApprovalLines = new List<FeeApprovalRequestedLineDto>();

        // ─── Appointment change ────────────────────────────────────────────────
        if (command.AppointmentId.HasValue && command.NewAppointmentDate.HasValue)
        {
            var appointment = await dbContext.Appointments
                .Include(a => a.History)
                .FirstOrDefaultAsync(a => a.Id == command.AppointmentId.Value, cancellationToken)
                ?? throw new NotFoundException("Appointment", command.AppointmentId.Value);

            // Verify appointment belongs to the named assignment (not just any appraisal assignment)
            if (appointment.AssignmentId != command.AssignmentId)
                throw new InvalidOperationException("Appointment does not belong to the specified assignment");

            appointment.Reschedule(command.RequestedBy, command.NewAppointmentDate.Value);

            requiresApprovalLines.Add(new FeeApprovalRequestedLineDto(
                "Appointment",
                appointment.Id,
                command.NewAppointmentDate.Value,
                appointment.RescheduleCount,
                null, null, null));

            logger.LogInformation(
                "Rescheduled appointment {AppointmentId} for appraisal {AppraisalId} by company {CompanyId}",
                appointment.Id, command.AppraisalId, command.RequestedByCompanyId);
        }

        // ─── Fee additions ─────────────────────────────────────────────────────
        if (command.FeeLines.Count > 0)
        {
            var fee = await dbContext.AppraisalFees
                .Include(f => f.Items)
                .FirstOrDefaultAsync(f => f.AssignmentId == command.AssignmentId, cancellationToken);

            if (fee is null)
            {
                fee = AppraisalFee.Create(command.AssignmentId);
                dbContext.AppraisalFees.Add(fee);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            foreach (var line in command.FeeLines)
            {
                var item = fee.AddItem(line.FeeCode, line.FeeDescription, line.FeeAmount, requiresApproval: true);

                requiresApprovalLines.Add(new FeeApprovalRequestedLineDto(
                    "Fee",
                    item.Id,
                    null, null,
                    line.FeeCode,
                    line.FeeDescription,
                    line.FeeAmount));
            }

            logger.LogInformation(
                "Added {Count} pending fee item(s) for assignment {AssignmentId} by company {CompanyId}",
                command.FeeLines.Count, command.AssignmentId, command.RequestedByCompanyId);
        }

        // ─── Publish cross-module approval request ─────────────────────────────
        if (requiresApprovalLines.Count > 0)
        {
            outbox.Publish(
                new FeeAppointmentApprovalRequestedIntegrationEvent
                {
                    AppraisalId = command.AppraisalId,
                    RequestSource = "Ext",
                    Lines = requiresApprovalLines
                },
                correlationId: command.AppraisalId.ToString());

            logger.LogInformation(
                "Published FeeAppointmentApprovalRequested for appraisal {AppraisalId}, {Count} lines",
                command.AppraisalId, requiresApprovalLines.Count);
        }

        return Unit.Value;
    }
}
