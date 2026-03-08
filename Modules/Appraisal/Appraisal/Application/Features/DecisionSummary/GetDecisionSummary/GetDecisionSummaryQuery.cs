using Shared.CQRS;

namespace Appraisal.Application.Features.DecisionSummary.GetDecisionSummary;

public record GetDecisionSummaryQuery(Guid AppraisalId) : IQuery<GetDecisionSummaryResult>;
