using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Shared.CQRS;
using Shared.Data;
using Shared.Time;

namespace Appraisal.Application.Features.DecisionSummary.SaveDecisionSummary;

public class SaveDecisionSummaryCommandHandler(
    IAppraisalDecisionRepository decisionRepository,
    AppraisalDbContext db,
    ISqlConnectionFactory connectionFactory,
    ForceSaleRateResolver forceSaleRateResolver,
    AppraisalDateResolver appraisalDateResolver,
    IDateTimeProvider dateTimeProvider
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
            command.ExternalAppraiserOpinionType,
            command.ExternalAppraiserOpinion,
            command.CommitteeOpinionType,
            command.CommitteeOpinion,
            command.InternalAppraiserOpinionType,
            command.InternalAppraiserOpinion,
            command.AdditionalAssumptions,
            command.HasConstructionLicenseDoc,
            command.HasConstructionProgressTableDoc,
            command.HasConstructionPhotoDoc);

        // Force-sale rate is no longer part of this form save — it's persisted immediately via
        // UpdateForceSaleRateCommandHandler on blur (two writers to one column risked a stale
        // overwrite: blur saves 50, user cancels so the form resets to 70, user Saves, 70 gets
        // written back over the 50 the user actually committed). Load the row FIRST and resolve
        // from the STORED override (valuation?.ForceSaleRate), never from this command, so an
        // unrelated field edit here can't silently reset a rate the user already committed.
        var valuation = db.ValuationAnalyses.Local
                            .FirstOrDefault(v => v.AppraisalId == command.AppraisalId)
                        ?? await db.ValuationAnalyses
                            .FirstOrDefaultAsync(v => v.AppraisalId == command.AppraisalId, cancellationToken);

        // Book Verification override: persist the 3 review values into ValuationAnalyses.
        // When IsPriceVerified != true, zero out all 3 fields.
        // When verified, null TotalAppraisalPriceReview is persisted as 0m (not skipped).
        //
        // NOTE: AppraisedValue/ForcedSaleValue/InsuranceValue are a SINGLE set of columns, written
        // by two different paths — AppraisalFinalValuesChangedEventHandler (live computed totals)
        // and this handler (Book Verification values). The read side aliases them as *Review.
        // They must therefore always be written as a coherent triple: recomputing ForcedSaleValue
        // here while leaving AppraisedValue at 0 would emit "force-sale 1.27M of appraised 0" to
        // Collateral master and the AS400 feed. A rate-only change consequently reaches
        // ForcedSaleValue when the event handler next fires.
        decimal appraisedReview;
        decimal forcedReview;
        decimal insuranceReview;

        if (command.IsPriceVerified == true)
        {
            appraisedReview = command.TotalAppraisalPriceReview ?? 0m;
            var (insurance, block) = await ComputeInsuranceAndBlockSumsAsync(
                command.AppraisalId, cancellationToken);
            insuranceReview = insurance;
            // A block's forced sale scales with the typed total by the units' own ratio (see
            // BlockForcedSaleReview); the override rate only applies to a normal appraisal.
            forcedReview = block is not null
                ? BlockForcedSaleReview(
                    appraisedReview, block.UnitTotal, block.ForcedSale,
                    block.UnitTotal > 0
                        ? 0m   // unused: the units' own ratio applies
                        : await forceSaleRateResolver.ResolveBlockEffectivePercentAsync(
                            command.AppraisalId, cancellationToken))
                : Math.Round(
                    appraisedReview
                    * await forceSaleRateResolver.ResolveAsync(
                        command.AppraisalId, valuation?.ForceSaleRate, cancellationToken)
                    / 100m / 1000,
                    MidpointRounding.AwayFromZero) * 1000;
        }
        else
        {
            appraisedReview = 0m;
            forcedReview = 0m;
            insuranceReview = 0m;
        }

        if (valuation is null)
        {
            // ValuationDate is derived from the appraisal's appointments, NOT stamped with "now".
            // Saving the decision summary is not an appraisal event, but this is the first writer of
            // the row when no pricing has been entered — and ValuationDate leads every appraisal-date
            // read surface (the printed book, both AS400 feeds, History Search, the reappraisal list's
            // last-appraisal date), so "now" would publish a save timestamp as the appraisal date.
            // ApplicationNow remains only when no appointment exists to derive from.
            var seedDate = await appraisalDateResolver
                               .ResolveFromAppointmentsAsync(command.AppraisalId, cancellationToken)
                           ?? dateTimeProvider.ApplicationNow;

            valuation = ValuationAnalysis.Create(command.AppraisalId, "Combined", seedDate);
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
            decision.ExternalAppraiserOpinionType,
            decision.ExternalAppraiserOpinion,
            decision.CommitteeOpinionType,
            decision.CommitteeOpinion,
            decision.InternalAppraiserOpinionType,
            decision.InternalAppraiserOpinion,
            appraisedReview,
            decision.AdditionalAssumptions,
            decision.HasConstructionLicenseDoc,
            decision.HasConstructionProgressTableDoc,
            decision.HasConstructionPhotoDoc);
    }

    /// <summary>
    /// Block Book Verification forced sale for a typed appraised total: the typed total times the units' own
    /// forced-sale-to-appraisal ratio, rounded to the nearest 1,000. With no unit total, the project's effective
    /// % stands in for the ratio. When the typed total equals the unit total the result is exactly the unit sum
    /// (not rounded, so an imported price list reaches the review as stored).
    /// </summary>
    internal static decimal BlockForcedSaleReview(
        decimal appraisedReview, decimal unitTotal, decimal unitForcedSale, decimal effectivePercent)
    {
        if (unitTotal > 0 && appraisedReview == unitTotal) return unitForcedSale;

        // Multiply first, divide last: a repeating ratio (4.1M / 6M) truncated in decimal would push an exact
        // x,500 result below the half and round it the wrong way.
        var scaled = unitTotal > 0
            ? appraisedReview * unitForcedSale / unitTotal
            : appraisedReview * effectivePercent / 100m;
        return Math.Round(scaled / 1000, MidpointRounding.AwayFromZero) * 1000;
    }

    /// <summary>
    /// Insurance for the review, plus the unit sums when the appraisal is a block (null otherwise).
    /// Block: plain sums over every unit's stored prices, matching ValuationAnalyses.
    /// </summary>
    private async Task<(decimal Insurance, BlockSums? Block)> ComputeInsuranceAndBlockSumsAsync(
        Guid appraisalId, CancellationToken ct)
    {
        var blockParam = new DynamicParameters();
        blockParam.Add("AppraisalId", appraisalId);

        const string projectProbeSql = """
            SELECT TOP 1 Id FROM appraisal.Projects WHERE AppraisalId = @AppraisalId
            """;

        var projectId = await connectionFactory.QueryFirstOrDefaultAsync<Guid?>(projectProbeSql, blockParam);

        if (projectId.HasValue)
        {
            // Every unit counts, sold or not — consistent with the unit-price appraisal and the
            // Decision Summary block totals. Plain sums, not rounded: they must equal
            // ValuationAnalyses (an imported price list is kept verbatim).
            const string blockSumsSql = """
                SELECT ISNULL(SUM(pup.CoverageAmount), 0) AS Insurance,
                       ISNULL(SUM(pup.ForceSellingPrice), 0) AS ForcedSale,
                       ISNULL(SUM(pup.TotalAppraisalValueRounded), 0) AS UnitTotal
                FROM appraisal.ProjectUnitPrices pup
                JOIN appraisal.ProjectUnits pu ON pu.Id = pup.ProjectUnitId
                JOIN appraisal.Projects p ON p.Id = pu.ProjectId
                WHERE p.AppraisalId = @AppraisalId
                """;
            var sums = await connectionFactory.QueryFirstOrDefaultAsync<BlockSums>(blockSumsSql, blockParam)
                       ?? new BlockSums(0m, 0m, 0m);
            return (sums.Insurance, sums);
        }

        return (await BuildingInsuranceCalculator.ComputeAsync(connectionFactory, appraisalId), null);
    }

    private sealed record BlockSums(decimal Insurance, decimal ForcedSale, decimal UnitTotal);
}
