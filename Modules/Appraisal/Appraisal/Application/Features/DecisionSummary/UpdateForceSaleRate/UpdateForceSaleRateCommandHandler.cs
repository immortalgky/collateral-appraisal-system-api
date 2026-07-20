using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Appraisal.Application.Features.DecisionSummary.UpdateForceSaleRate;

/// <summary>
/// Persists a rate-only edit to the force-sale override and immediately rewrites the derived
/// ForcedSaleValue, so PDF providers / the external book letter / Integration GetAppraisalResult /
/// the AS400 feed never serve a stale figure between now and the next PropertyGroup pricing change
/// (the only other writer of this column — see AppraisalFinalValuesChangedEventHandler).
/// </summary>
public class UpdateForceSaleRateCommandHandler(
    AppraisalDbContext db,
    ForceSaleRateResolver forceSaleRateResolver
) : ICommandHandler<UpdateForceSaleRateCommand>
{
    public async Task<Unit> Handle(UpdateForceSaleRateCommand command, CancellationToken cancellationToken)
    {
        var valuation = db.ValuationAnalyses.Local
                            .FirstOrDefault(v => v.AppraisalId == command.AppraisalId)
                        ?? await db.ValuationAnalyses
                            .FirstOrDefaultAsync(v => v.AppraisalId == command.AppraisalId, cancellationToken);

        if (valuation is null)
        {
            // No ValuationAnalyses row yet means no PropertyGroup pricing has been entered for
            // this appraisal, but the override still has to live somewhere — create the row to
            // hold it. Deliberately do NOT call UpdateSummary here: there is no AppraisedValue to
            // derive a ForcedSaleValue from yet, and AppraisedValue/InsuranceValue/ForcedSaleValue
            // are left at Create's defaults (0 / null / null). Leaving ForcedSaleValue NULL (not
            // 0m) matters — reporting's fallback is `valuation?.ForcedSaleValue ?? computed`, so
            // null correctly falls through to the computed value; writing 0m would pin reports to
            // zero. The next pricing change fires AppraisalFinalValuesChangedEventHandler, which
            // fills in the real values while preserving this override (it reads row.ForceSaleRate).
            valuation = ValuationAnalysis.Create(command.AppraisalId, "Combined", DateTime.Now);
            db.ValuationAnalyses.Add(valuation);
            valuation.SetForceSaleRate(command.ForceSellingRateOverride);
            return Unit.Value;
        }

        valuation.SetForceSaleRate(command.ForceSellingRateOverride);

        // FSP is always DERIVED, never hand-entered — the appraised total and insurance value do
        // NOT depend on the rate, so they pass through unchanged. Recomputing ForcedSaleValue from
        // the CURRENTLY STORED AppraisedValue keeps the triple coherent by construction, whether
        // that stored value is a computed total (AppraisalFinalValuesChangedEventHandler) or a
        // verifier's Book Verification override (SaveDecisionSummaryCommandHandler).
        var rate = await forceSaleRateResolver.ResolveAsync(
            command.AppraisalId, command.ForceSellingRateOverride, cancellationToken);

        valuation.UpdateSummary(
            valuation.ValuationApproach,
            valuation.ValuationDate,
            valuation.AppraisedValue,
            valuation.AppraisedValue * rate / 100m,
            valuation.InsuranceValue);

        return Unit.Value;
    }
}
