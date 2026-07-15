namespace Common.Application.Features.Monitoring.GetPendingEvaluationStaff;

/// <summary>
/// Autocomplete option for the internal-followup-staff filter on the Pending Evaluation tab.
/// Value is the staff username (matches the list filter's InternalFollowupStaff param);
/// Label is the resolved display name.
/// </summary>
public record InternalFollowupStaffOption(string Value, string Label);
