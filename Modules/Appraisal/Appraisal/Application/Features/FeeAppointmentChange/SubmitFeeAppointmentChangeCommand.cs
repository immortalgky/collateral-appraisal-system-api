using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.FeeAppointmentChange;

/// <summary>
/// Submitted by an external company to request a combined appointment + fee change
/// while holding the active ext-appraisal-assignment task.
///
/// Components not requiring approval are applied immediately (Approve).
/// Components requiring approval are bundled into a single RaiseFeeAppointmentApprovalCommand
/// dispatched cross-module via the integration-event outbox.
/// </summary>
public record SubmitFeeAppointmentChangeCommand(
    Guid AppraisalId,
    Guid AssignmentId,

    // Optional appointment change
    Guid? AppointmentId,
    DateTime? NewAppointmentDate,

    // Optional fee additions (one or more)
    IReadOnlyList<SubmitFeeChangeLineDto> FeeLines,

    /// <summary>The external company's authenticated company_id claim.</summary>
    string RequestedByCompanyId,

    string RequestedBy
) : ICommand<Unit>, ITransactionalCommand<IAppraisalUnitOfWork>;

public record SubmitFeeChangeLineDto(
    string FeeCode,
    string FeeDescription,
    decimal FeeAmount);
