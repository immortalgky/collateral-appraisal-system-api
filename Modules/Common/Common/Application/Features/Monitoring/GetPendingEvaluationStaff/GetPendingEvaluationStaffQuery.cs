using Shared.CQRS;

namespace Common.Application.Features.Monitoring.GetPendingEvaluationStaff;

public record GetPendingEvaluationStaffQuery : IQuery<IReadOnlyList<InternalFollowupStaffOption>>;
