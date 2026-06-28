using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Collateral.Contracts.ConstructionInspection;
using MediatR;
using Parameter.Contracts.Parameters;
using Parameter.Contracts.Parameters.Dtos;

namespace Appraisal.Application.Services;

/// <summary>
/// Materialises fee items on the AppraisalFee shell once the real assignee is known.
/// Handles three paths: tier-based (internal/external manual), quotation-price, and
/// construction-inspection (seeded from prior engagement, bypasses tier/quotation).
/// Idempotent — safe to call multiple times for the same assignment.
/// </summary>
public class AssignmentFeeService(
    AppraisalDbContext dbContext,
    ISender mediator,
    IParameterLookupService parameterLookup,
    ILogger<AssignmentFeeService> logger) : IAssignmentFeeService
{
    // Bank absorbs the full customer bill for these payment types.
    private static readonly HashSet<string> FullAbsorbFeePaymentTypes = new(StringComparer.Ordinal) { "05", "06", "07" };

    // Fee names are maintained in the TypeOfFee parameter group, resolved by code.
    private const string FeeTypeParameterGroup = "TypeOfFee";

    // Resolves the English fee-type description for a code; falls back to the code itself.
    private async Task<string> ResolveFeeNameAsync(string feeCode, CancellationToken ct)
    {
        var description = await parameterLookup.GetDescriptionAsync(
            new ParameterDto(null, FeeTypeParameterGroup, null, "EN", feeCode, null, true, null), ct);
        return string.IsNullOrWhiteSpace(description) ? feeCode : description;
    }

    public async Task EnsureAssignmentFeeItemsAsync(
        Guid appraisalId,
        Guid assignmentId,
        AssignmentFeeSource source,
        CancellationToken ct)
    {
        // Step 1 — Locate the fee shell for this assignment.
        // If no shell exists (reassignment that created a fresh assignment row), copy context
        // fields from the latest existing fee on the appraisal and create a new shell.
        var fee = await dbContext.AppraisalFees
            .Include(f => f.Items)
            .FirstOrDefaultAsync(f => f.AssignmentId == assignmentId, ct);

        if (fee is null)
        {
            var latestFee = await dbContext.AppraisalFees
                .Where(f => f.AssignmentId != assignmentId)
                .Join(
                    dbContext.AppraisalAssignments.Where(a => a.AppraisalId == appraisalId),
                    f => f.AssignmentId,
                    a => a.Id,
                    (f, _) => f)
                .OrderByDescending(f => f.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (latestFee is null)
            {
                logger.LogWarning(
                    "No AppraisalFee shell found for AssignmentId={AssignmentId} on AppraisalId={AppraisalId}. Skipping fee materialisation.",
                    assignmentId, appraisalId);
                return;
            }

            fee = AppraisalFee.Create(
                assignmentId: assignmentId,
                feePaymentType: latestFee.FeePaymentType,
                feeNotes: latestFee.FeeNotes,
                totalSellingPrice: latestFee.TotalSellingPrice);

            if (latestFee.BankAbsorbAmount > 0)
                fee.SetBankAbsorb(latestFee.BankAbsorbAmount);

            dbContext.AppraisalFees.Add(fee);

            logger.LogInformation(
                "Created new AppraisalFee shell {FeeId} for reassigned AssignmentId={AssignmentId} on AppraisalId={AppraisalId} (copied context from fee {SourceFeeId})",
                fee.Id, assignmentId, appraisalId, latestFee.Id);
        }

        // Step 2 — Idempotency: if items already exist, nothing to do.
        if (fee.HasItems)
        {
            logger.LogInformation(
                "AppraisalFee {FeeId} for AssignmentId={AssignmentId} already has items. Skipping.",
                fee.Id, assignmentId);
            return;
        }

        // Step 3 — Add the appropriate item based on source.
        switch (source)
        {
            case AssignmentFeeSource.TierBased:
            {
                var totalSellingPrice = fee.TotalSellingPrice ?? 0m;

                var tiers = await dbContext.FeeStructures
                    .Where(fs => fs.IsActive && fs.FeeCode == "01")
                    .OrderBy(fs => fs.MinSellingPrice)
                    .ToListAsync(ct);

                var matched = tiers.FirstOrDefault(t => t.IsApplicableFor(totalSellingPrice));
                if (matched is null)
                {
                    matched = tiers.OrderByDescending(t => t.MinSellingPrice).First();
                    logger.LogWarning(
                        "No fee tier matched TotalSellingPrice {TotalSellingPrice} for AppraisalFee {FeeId}. Falling back to highest tier (BaseAmount={BaseAmount})",
                        totalSellingPrice, fee.Id, matched.BaseAmount);
                }

                var feeName = await ResolveFeeNameAsync(matched.FeeCode, ct);
                fee.AddItem(matched.FeeCode, feeName, matched.BaseAmount);

                logger.LogInformation(
                    "Appraisal fee created: fee {FeeId} assigned tier item (FeeCode={FeeCode}, BaseAmount={BaseAmount}) for AssignmentId={AssignmentId} (TotalSellingPrice={TotalSellingPrice})",
                    fee.Id, matched.FeeCode, matched.BaseAmount, assignmentId, totalSellingPrice);
                break;
            }

            case AssignmentFeeSource.Quotation quotationSource:
            {
                // Use feeCode "01" (Appraisal Fee) so the FE renders this row as
                // non-deletable (FeeInformationSection.tsx gates delete on feeCode !== '01').
                // The amount is ex-VAT — RecalculateFromItems will add VAT on top.
                var rfqLabel = !string.IsNullOrWhiteSpace(quotationSource.QuotationNumber)
                    ? quotationSource.QuotationNumber
                    : quotationSource.QuotationRequestId.ToString();

                fee.AddItem(
                    feeCode: "01",
                    feeDescription: $"Appraisal fee agreed via competitive quotation {rfqLabel}",
                    feeAmount: quotationSource.Amount);

                logger.LogInformation(
                    "Created quotation fee: fee {FeeId} assigned quotation item (Amount={Amount}, Rfq={Rfq}) for AssignmentId={AssignmentId}",
                    fee.Id, quotationSource.Amount, rfqLabel, assignmentId);
                break;
            }

            case AssignmentFeeSource.ConstructionInspection ciSource:
            {
                // CI bypasses tier/quotation. If no prior engagement carries a CI fee,
                // leave the fee items empty per spec (no fallback to tier).
                if (ciSource.Amount is null or <= 0m)
                {
                    logger.LogInformation(
                        "Construction Inspection fee source has no amount for AppraisalId={AppraisalId}. Leaving fee items empty.",
                        appraisalId);
                    return;
                }

                // Use feeCode "01" so FE renders the row non-deletable, consistent with
                // the quotation path. Amount is ex-VAT — RecalculateFromItems adds VAT on top.
                fee.AddItem(
                    feeCode: "01",
                    feeDescription: "Construction inspection fee from prior engagement",
                    feeAmount: ciSource.Amount.Value);

                logger.LogInformation(
                    "Construction Inspection fee created: fee {FeeId} assigned CI item (Amount={Amount}) for AssignmentId={AssignmentId}",
                    fee.Id, ciSource.Amount.Value, assignmentId);
                break;
            }

            default:
                logger.LogWarning(
                    "Unknown AssignmentFeeSource type {SourceType} for AssignmentId={AssignmentId}. Skipping.",
                    source.GetType().Name, assignmentId);
                return;
        }

        // Step 4 — Finalise bank absorb. For payment types 05/06/07 the bank absorbs the full
        // customer bill (TotalFeeAfterVAT), overriding any user-entered AbsorbedAmount. For other
        // types, re-apply whatever was captured on the shell so CustomerPayableAmount is correct.
        // AddItem already called RecalculateFromItems; SetBankAbsorb re-triggers it.
        if (fee.FeePaymentType is { } paymentType && FullAbsorbFeePaymentTypes.Contains(paymentType))
        {
            fee.SetBankAbsorb(fee.TotalFeeAfterVAT);
            logger.LogInformation(
                "Full-absorb applied to fee {FeeId}: FeePaymentType={FeePaymentType}, BankAbsorbAmount={BankAbsorbAmount}",
                fee.Id, paymentType, fee.BankAbsorbAmount);
        }
        else if (fee.BankAbsorbAmount > 0)
        {
            fee.SetBankAbsorb(fee.BankAbsorbAmount);
        }
    }

    public async Task<AssignmentFeeSource> ResolveSourceForAppraisalAsync(
        Domain.Appraisals.Appraisal appraisal,
        AssignmentFeeSource defaultSource,
        CancellationToken ct)
    {
        if (appraisal.AppraisalType != AppraisalTypes.Progressive ||
            appraisal.PrevAppraisalId is not { } prevId)
        {
            return defaultSource;
        }

        var ciFee = await mediator.Send(
            new GetConstructionInspectionFeeForAppraisalQuery(prevId), ct);

        logger.LogInformation(
            "Resolved Construction Inspection fee source for AppraisalId={AppraisalId} from PrevAppraisalId={PrevAppraisalId}: Amount={Amount}",
            appraisal.Id, prevId, ciFee);

        return new AssignmentFeeSource.ConstructionInspection(ciFee);
    }
}
