namespace Appraisal.Application.Features.Appraisals.SuspendAppraisal;

public record SuspendAppraisalResult(Guid AppraisalId, bool IsSuspended, DateTime ActionAt, string ActionBy);
