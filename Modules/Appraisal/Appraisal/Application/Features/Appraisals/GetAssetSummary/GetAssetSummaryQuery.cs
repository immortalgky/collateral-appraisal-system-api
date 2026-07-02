using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetAssetSummary;

public record GetAssetSummaryQuery(Guid AppraisalId) : IQuery<GetAssetSummaryResult>;
