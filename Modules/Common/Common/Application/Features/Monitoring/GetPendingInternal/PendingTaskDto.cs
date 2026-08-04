namespace Common.Application.Features.Monitoring.GetPendingInternal;

public record PendingTaskDto(
    Guid PendingTaskId,
    Guid? AppraisalId,
    string? AppraisalNumber,
    string? CustomerName,
    string? TaskType,
    string? TaskDescription,
    string? Purpose,
    string? PropertyType,
    string? SlaStatus,
    string? Priority,
    DateTime? RequestedDate,
    DateTime? AssignedDate,
    string? PIC,
    string? Movement,
    int? OlaTargetHours,
    int? OlaActualHours,
    int? OlaVarianceHours,
    string? ActivityId,
    string? AppraisalCompanyName,
    // Thai name (null when absent); the client picks by its own locale. Position is load-bearing —
    // it must stay directly after AppraisalCompanyName to match the handlers' SELECT order.
    string? AppraisalCompanyNameLocal,
    string MonitoringType,
    string? AssignedTo,
    string? AssignedType,
    DateTime? OpenDate,
    DateTime? AppointmentDate,
    int? SlaDurationHours,
    string? AppraisalStatus
);
