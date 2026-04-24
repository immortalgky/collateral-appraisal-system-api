using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;

namespace Appraisal.Application.Services;

/// <summary>
/// Materialises fee items on the AppraisalFee shell once the real assignee is known.
/// Handles three paths: tier-based (internal/external manual), and quotation-price.
/// Idempotent — safe to call multiple times for the same assignment.
/// </summary>
public class AssignmentFeeService(
    AppraisalDbContext dbContext,
    ILogger<AssignmentFeeService> logger) : IAssignmentFeeService
{
    // Bank absorbs the full customer bill for these payment types.
    private static readonly HashSet<string> FullAbsorbFeePaymentTypes = new(StringComparer.Ordinal) { "05", "06", "07" };


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

                fee.AddItem(matched.FeeCode, matched.FeeName, matched.BaseAmount);

                logger.LogInformation(
                    "Appraisal fee created: fee {FeeId} assigned tier item (FeeCode={FeeCode}, BaseAmount={BaseAmount}) for AssignmentId={AssignmentId} (TotalSellingPrice={TotalSellingPrice})",
                    fee.Id, matched.FeeCode, matched.BaseAmount, assignmentId, totalSellingPrice);
                break;
            }

            case AssignmentFeeSource.Quotation(var amount, var rfqId):
            {
                fee.AddItem(
                    feeCode: "QUOTATION_FEE",
                    feeDescription: $"Appraisal fee agreed via competitive quotation RFQ {rfqId}",
                    feeAmount: amount);

                logger.LogInformation(
                    "Created quotation fee: fee {FeeId} assigned quotation item (Amount={Amount}, RfqId={RfqId}) for AssignmentId={AssignmentId}",
                    fee.Id, amount, rfqId, assignmentId);
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
}
