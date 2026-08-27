using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Parameter.Contracts.Parameters;
using Parameter.Contracts.Parameters.Dtos;

namespace Appraisal.Application.Services;

/// <summary>
/// Materialises fee items on the AppraisalFee shell once the real assignee is known.
/// Handles three paths: tier-based (internal/external manual), quotation-price, and
/// construction-inspection (chained from the prior appraisal's own fee, bypasses tier/quotation).
/// Idempotent — safe to call multiple times for the same assignment.
/// </summary>
public class AssignmentFeeService(
    AppraisalDbContext dbContext,
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
            case AssignmentFeeSource.TierBased tierSource:
            {
                var totalSellingPrice = fee.TotalSellingPrice ?? 0m;
                var appraisalType = tierSource.AppraisalType;

                var tiers = await dbContext.FeeStructures
                    .Where(fs => fs.IsActive && fs.FeeCode == "01"
                                 && (fs.AppraisalType == null || fs.AppraisalType == appraisalType))
                    .OrderBy(fs => fs.MinSellingPrice)
                    .ToListAsync(ct);

                // A type-scoped ladder replaces the generic one outright rather than merging with
                // it — otherwise a flat per-type rate (e.g. PreAppraisal 10,000) would be silently
                // outranked by a generic selling-price band.
                var candidates = tiers.Where(t => t.AppraisalType != null).ToList();
                if (candidates.Count == 0)
                    candidates = tiers;

                if (candidates.Count == 0)
                {
                    logger.LogError(
                        "No active fee tier (FeeCode=01) configured for AppraisalType={AppraisalType}. Leaving fee {FeeId} without items for AssignmentId={AssignmentId}.",
                        appraisalType, fee.Id, assignmentId);
                    return;
                }

                var matched = candidates.FirstOrDefault(t => t.IsApplicableFor(totalSellingPrice));
                if (matched is null)
                {
                    matched = candidates.OrderByDescending(t => t.MinSellingPrice).First();
                    logger.LogWarning(
                        "No fee tier matched TotalSellingPrice {TotalSellingPrice} (AppraisalType={AppraisalType}) for AppraisalFee {FeeId}. Falling back to highest tier (BaseAmount={BaseAmount})",
                        totalSellingPrice, appraisalType, fee.Id, matched.BaseAmount);
                }

                var feeName = await ResolveFeeNameAsync(matched.FeeCode, ct);
                fee.AddItem(matched.FeeCode, feeName, matched.BaseAmount);

                logger.LogInformation(
                    "Appraisal fee created: fee {FeeId} assigned tier item (FeeCode={FeeCode}, BaseAmount={BaseAmount}, TierAppraisalType={TierAppraisalType}) for AssignmentId={AssignmentId} (AppraisalType={AppraisalType}, TotalSellingPrice={TotalSellingPrice})",
                    fee.Id, matched.FeeCode, matched.BaseAmount, matched.AppraisalType, assignmentId, appraisalType, totalSellingPrice);
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
                // CI bypasses tier/quotation. If the prior appraisal carries no CI fee,
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
                    feeDescription: "Construction inspection fee from prior appraisal",
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
            // Stamp the appraisal type onto a tier lookup so it can pick the type-scoped ladder
            // (e.g. the flat PreAppraisal/block rate). Quotation sources bypass tiers entirely and
            // are returned untouched.
            return defaultSource is AssignmentFeeSource.TierBased
                ? new AssignmentFeeSource.TierBased(appraisal.AppraisalType)
                : defaultSource;
        }

        var ciFee = await ResolveChainedInspectionFeeAsync(prevId, ct);

        logger.LogInformation(
            "Resolved Construction Inspection fee source for AppraisalId={AppraisalId} from PrevAppraisalId={PrevAppraisalId}: Amount={Amount}",
            appraisal.Id, prevId, ciFee);

        return new AssignmentFeeSource.ConstructionInspection(ciFee);
    }

    /// <summary>
    /// The inspection fee the NEXT Progressive appraisal should charge, read from the prior
    /// appraisal's own fee rows rather than from its CollateralEngagement.
    ///
    /// The engagement column this replaces was only ever a copy of exactly this calculation, taken
    /// at completion time by GetAppraisalForCollateralQueryHandler. Reading the origin keeps the
    /// chain (original -> 1st inspection -> 2nd -> ...) intact even where no collateral master
    /// exists, and removes the dependency on that row having been materialised first.
    ///
    /// Returns null when the prior appraisal, its assignment or its fee row is missing — the caller
    /// leaves the fee empty in that case, deliberately, with no fallback to the tier ladder.
    /// </summary>
    private async Task<decimal?> ResolveChainedInspectionFeeAsync(
        Guid prevAppraisalId,
        CancellationToken ct)
    {
        var prior = await dbContext.Appraisals
            .AsNoTracking()
            .Include(a => a.Assignments)
            .FirstOrDefaultAsync(a => a.Id == prevAppraisalId, ct);

        if (prior is null)
        {
            logger.LogWarning(
                "Construction Inspection fee: prior appraisal {PrevAppraisalId} not found; leaving the fee empty.",
                prevAppraisalId);
            return null;
        }

        // Same assignment election as the engagement writer: the newest one that was not rejected or
        // cancelled, because a re-assigned case must bill from the assignment that did the work.
        var latestAssignment = prior.Assignments
            .Where(a => a.AssignmentStatus.Code != AssignmentStatus.Rejected.Code
                        && a.AssignmentStatus.Code != AssignmentStatus.Cancelled.Code)
            .OrderByDescending(a => a.AssignedAt)
            .ThenByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .FirstOrDefault();

        if (latestAssignment is null)
            return null;

        // A Progressive appraisal quotes nothing for the next round — it charges through its own
        // fee line, so the next inspection inherits that. Sum only FeeCode "01" (the appraisal-fee
        // line, ex-VAT) so travel ("02") and urgent ("03") surcharges do not propagate down the chain.
        if (prior.AppraisalType == AppraisalTypes.Progressive)
        {
            return await dbContext.AppraisalFees
                .AsNoTracking()
                .Where(f => f.AssignmentId == latestAssignment.Id)
                .Select(f => (decimal?)f.Items.Where(i => i.FeeCode == "01").Sum(i => i.FeeAmount))
                .FirstOrDefaultAsync(ct);
        }

        // An original appraisal carries the quoted future-inspection fee explicitly, set via
        // UpdateConstructionInspectionFeeCommand. Null when the user never filled it in.
        return await dbContext.AppraisalFees
            .AsNoTracking()
            .Where(f => f.AssignmentId == latestAssignment.Id)
            .Select(f => f.ConstructionInspectionFeeAmount)
            .FirstOrDefaultAsync(ct);
    }
}
