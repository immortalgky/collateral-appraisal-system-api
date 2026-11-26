namespace Appraisal.Application.Features.Appraisals.GetAppraisalSuspensionLogs;

/// <summary>Suspend/reactivate history of one appraisal, newest first.</summary>
public record GetAppraisalSuspensionLogsQuery(Guid AppraisalId)
    : IQuery<IReadOnlyList<AppraisalSuspensionLogDto>>;

public record AppraisalSuspensionLogDto(
    string Action,
    string Reason,
    DateTime ActionAt,
    string ActionBy
);
