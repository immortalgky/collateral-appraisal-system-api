using Workflow.Contracts.FeeAppointmentApprovals;

namespace Appraisal.Application.Features.Appointments.CreateAppointment;

public class CreateAppointmentCommandHandler(
    IAppraisalRepository appraisalRepository,
    AppraisalDbContext dbContext,
    ISender sender)
    : ICommandHandler<CreateAppointmentCommand, CreateAppointmentResult>
{
    public async Task<CreateAppointmentResult> Handle(
        CreateAppointmentCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(command.AppraisalId, cancellationToken)
                        ?? throw new NotFoundException("Appraisal", command.AppraisalId);

        var assignment = appraisal.Assignments
                             .FirstOrDefault(a => a.AssignmentStatus == AssignmentStatus.Assigned
                                                  || a.AssignmentStatus == AssignmentStatus.InProgress
                                                  || a.AssignmentStatus == AssignmentStatus.Pending)
                         ??
                         throw new InvalidOperationException("No active assignment found for this appraisal.");

        var hasActiveAppointment = await dbContext.Appointments
            .AnyAsync(a => a.AssignmentId == assignment.Id
                           && (a.Status == "Pending" || a.Status == "Approved"),
                cancellationToken);

        if (hasActiveAppointment)
            throw new InvalidOperationException(
                "This assignment already has an active appointment. Complete or cancel it before creating a new one.");

        var appointment = Appointment.Create(
            assignment.Id,
            command.AppointmentDateTime,
            command.AppointedBy,
            command.LocationDetail,
            command.ContactPerson,
            command.ContactPhone);

        dbContext.Appointments.Add(appointment);

        // Evaluate policy at creation time (read-only cross-module query)
        var verdict = await sender.Send(
            new EvaluateFeeAppointmentApprovalQuery(
                command.AppraisalId,
                RequestSource: "Ext",
                ProposedAppointmentDate: command.AppointmentDateTime,
                RescheduleCount: appointment.RescheduleCount,
                CumulativeAddedFeeTotal: null),
            cancellationToken);

        if (!verdict.AppointmentRequiresApproval)
        {
            // Auto-apply: no approval needed — approve immediately
            appointment.Approve("system");
        }
        else
        {
            // Flag as requiring approval — user must call Submit for Approval
            appointment.FlagRequiresApproval();
        }

        return new CreateAppointmentResult(appointment.Id);
    }
}
