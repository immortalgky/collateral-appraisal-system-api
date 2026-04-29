using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shared.CQRS;

namespace Appraisal.Application.Features.DecisionSummary.SaveDecisionSummary;

public class SaveDecisionSummaryCommandHandler(
    IAppraisalDecisionRepository decisionRepository,
    AppraisalDbContext db
) : ICommandHandler<SaveDecisionSummaryCommand, SaveDecisionSummaryResult>
{
    public async Task<SaveDecisionSummaryResult> Handle(
        SaveDecisionSummaryCommand command,
        CancellationToken cancellationToken)
    {
        var decision = await decisionRepository.GetByAppraisalIdAsync(
            command.AppraisalId, cancellationToken);

        if (decision is null)
        {
            decision = AppraisalDecision.Create(command.AppraisalId);
            await decisionRepository.AddAsync(decision, cancellationToken);
        }

        decision.Update(
            command.IsPriceVerified,
            command.ConditionType,
            command.Condition,
            command.RemarkType,
            command.Remark,
            command.AppraiserOpinionType,
            command.AppraiserOpinion,
            command.CommitteeOpinionType,
            command.CommitteeOpinion,
            command.AdditionalAssumptions);

        // Book Verification override: persist the 3 review values into ValuationAnalyses.
        // When IsPriceVerified != true, zero out all 3 fields.
        // When verified, null TotalAppraisalPriceReview is persisted as 0m (not skipped).
        decimal appraisedReview;
        decimal forcedReview;
        decimal insuranceReview;

        if (command.IsPriceVerified == true)
        {
            appraisedReview = command.TotalAppraisalPriceReview ?? 0m;
            forcedReview = appraisedReview * 0.70m;
            insuranceReview = await ComputeBuildingInsuranceAsync(command.AppraisalId, cancellationToken);
        }
        else
        {
            appraisedReview = 0m;
            forcedReview = 0m;
            insuranceReview = 0m;
        }

        var valuation = db.ValuationAnalyses.Local
                            .FirstOrDefault(v => v.AppraisalId == command.AppraisalId)
                        ?? await db.ValuationAnalyses
                            .FirstOrDefaultAsync(v => v.AppraisalId == command.AppraisalId, cancellationToken);

        if (valuation is null)
        {
            valuation = ValuationAnalysis.Create(command.AppraisalId, "Combined", DateTime.Now);
            db.ValuationAnalyses.Add(valuation);
        }

        valuation.UpdateSummary(
            valuation.ValuationApproach,
            valuation.ValuationDate,
            appraisedReview,
            forcedReview,
            insuranceReview);

        return new SaveDecisionSummaryResult(
            decision.Id,
            decision.AppraisalId,
            decision.IsPriceVerified,
            decision.ConditionType,
            decision.Condition,
            decision.RemarkType,
            decision.Remark,
            decision.AppraiserOpinionType,
            decision.AppraiserOpinion,
            decision.CommitteeOpinionType,
            decision.CommitteeOpinion,
            appraisedReview,
            decision.AdditionalAssumptions);
    }

    private async Task<decimal> ComputeBuildingInsuranceAsync(Guid appraisalId, CancellationToken ct)
    {
        // BuildingAppraisalDetail is owned by AppraisalProperty — reach via the nav.
        var properties = await db.AppraisalProperties
            .AsSplitQuery()
            .Where(ap => ap.AppraisalId == appraisalId)
            .ToListAsync(ct);

        return properties
            .Where(ap => ap.BuildingDetail != null)
            .SelectMany(ap => ap.BuildingDetail!.DepreciationDetails)
            .Where(d => d.IsBuilding)
            .Sum(d => d.PriceAfterDepreciation);
    }
}
