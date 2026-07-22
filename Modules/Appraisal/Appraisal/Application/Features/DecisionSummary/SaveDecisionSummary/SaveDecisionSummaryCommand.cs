using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.DecisionSummary.SaveDecisionSummary;

public record SaveDecisionSummaryCommand(
    Guid AppraisalId,
    bool? IsPriceVerified,
    string? ConditionType,
    string? Condition,
    string? RemarkType,
    string? Remark,
    string? ExternalAppraiserOpinionType,
    string? ExternalAppraiserOpinion,
    string? CommitteeOpinionType,
    string? CommitteeOpinion,
    string? InternalAppraiserOpinionType,
    string? InternalAppraiserOpinion,
    decimal? TotalAppraisalPriceReview,
    string? AdditionalAssumptions
) : ICommand<SaveDecisionSummaryResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
