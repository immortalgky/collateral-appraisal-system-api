namespace Appraisal.Application.Features.Appraisals.SuspendAppraisal;

public record SuspendAppraisalRequest(bool IsSuspended, string Reason);
