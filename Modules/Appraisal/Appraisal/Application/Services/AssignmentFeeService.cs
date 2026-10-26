using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Parameter.Contracts.Parameters;
using Parameter.Contracts.Parameters.Dtos;

namespace Appraisal.Application.Services;

/// <summary>
/// Materialises fee items on the AppraisalFee shell once the real assignee is known.
/// Handles three paths: tier-based (internal/external manual), quotation-price, and
/// construction-inspection (chained from the prior appraisal's own fee, bypasses tier/quotation).
/// An admin-entered base (FeeCode "01") item outranks all three. Idempotent — safe to call multiple times
/// for the same assignment; a system base left by an earlier pass is re-priced to the current source.
/// </summary>
public class AssignmentFeeService(
    AppraisalDbContext dbContext,
    IParameterLookupService parameterLookup,
    ILogger<AssignmentFeeService> logger) : IAssignmentFeeService
{
    // Bank absorbs the full customer bill for these payment types.
    private static readonly HashSet<string> FullAbsorbFeePaymentTypes = new(StringComparer.Ordinal) { "05", "06", "07" };

    // Resolves the English fee-type description for a code; falls back to the code itself.
    private async Task<string> ResolveFeeNameAsync(string feeCode, CancellationToken ct)
    {
        var description = await parameterLookup.GetDescriptionAsync(
            new ParameterDto(null, AppraisalFee.FeeTypeParameterGroup, null, "EN", feeCode, null, true, null), ct);
        return string.IsNullOrWhiteSpace(description) ? feeCode : description;
    }

    public async Task EnsureAssignmentFeeItemsAsync(
        Guid appraisalId,
        Guid assignmentId,
        AssignmentFeeSource source,
        bool repriceSystemBase,
        CancellationToken ct)
    {
        // Step 1 — Locate the fee shell for this assignment.
        // If no shell exists (reassignment that created a fresh assignment row), copy context
        // fields from the latest existing fee on the appraisal and create a new shell.
        var fee = await dbContext.AppraisalFees
            .Include(f => f.Items)
            .Where(f => f.AssignmentId == assignmentId)
            .OrderBy(f => f.Id) // unique, so a split query cannot pair the row with another row's items
            .FirstOrDefaultAsync(ct);

        if (fee is null)
        {
            var created = await FeeShellFactory.CreateFromLatestAsync(dbContext, appraisalId, assignmentId, ct);

            if (created is null)
            {
                WarnIfUnpricedQuotation(source, assignmentId);
                logger.LogWarning(
                    "No AppraisalFee shell found for AssignmentId={AssignmentId} on AppraisalId={AppraisalId}. Skipping fee materialisation.",
                    assignmentId, appraisalId);
                return;
            }

            fee = created.Value.Shell;

            logger.LogInformation(
                "Created new AppraisalFee shell {FeeId} for reassigned AssignmentId={AssignmentId} on AppraisalId={AppraisalId} (copied context from fee {SourceFeeId})",
                fee.Id, assignmentId, appraisalId, created.Value.SourceFeeId);
        }

        // The absorb the request asked for, read before any item recalculation clamps it to a partial total.
        var requestedAbsorb = fee.BankAbsorbAmount;
        var hadItems = fee.HasItems;

        // Step 2 — The base (FeeCode "01") line. An admin's override is never recomputed, and neither is any
        // other non-system 01. A system 01 is brought in line with the CURRENT source only on a genuine
        // (re)assignment (repriceSystemBase: the row was Pending, e.g. routed back to the admin queue); any other
        // call is a replay or a later route-back re-fire, which must not rewrite a base that may already be billed,
        // nor add one to a fee that already holds items (the original HasItems safety).
        var existingBase = fee.Items.FirstOrDefault(i => i.FeeCode == AppraisalFee.BaseFeeCode);
        var wroteBase = false;
        if (existingBase is not null && (existingBase.Source != FeeItemSource.System || !repriceSystemBase))
        {
            logger.LogInformation(
                "AppraisalFee {FeeId} for AssignmentId={AssignmentId} already has a {Source} base item. Not recomputing.",
                fee.Id, assignmentId, existingBase.Source);
        }
        else if (existingBase is null && fee.HasItems && !repriceSystemBase)
        {
            logger.LogInformation(
                "AppraisalFee {FeeId} for AssignmentId={AssignmentId} already has items and this is not a (re)assignment. Not adding a base.",
                fee.Id, assignmentId);
        }
        else if (WarnIfUnpricedQuotation(source, assignmentId))
        {
            // Never a 01 at 0. Not a throw: this runs after the workflow has already moved on (AssignAppraisal
            // judged the price before the resume), so a throw would only retry into the error queue and strand the
            // row Pending. Reaching here, any base is a system one being re-priced (repriceSystemBase), and the new
            // source prices nothing, so the previous source's tier base must not keep billing this company — the
            // fee preview drops it the same way.
            if (existingBase is not null)
                fee.RemoveItem(existingBase.Id);
        }
        else if (fee.IsBlank)
        {
            // The row an admin's first fee edit created because the request carried no fee data: assignment has always
            // billed nothing for such an appraisal (it skipped when no fee row existed), whatever the source.
            logger.LogInformation(
                "AppraisalFee {FeeId} for AssignmentId={AssignmentId} carries no request fee data. No base fee resolved.",
                fee.Id, assignmentId);
            // Same as above: a system base left by the previous source (re-assignment) must not survive.
            if (existingBase is not null)
                fee.RemoveItem(existingBase.Id);
        }
        else
        {
            // Step 3 — Resolve what the source yields and make it the base item.
            var resolved = await ResolveBaseFeeAsync(source, fee.TotalSellingPrice ?? 0m, ct);

            if (resolved.Amount is { } amount)
            {
                // Feecode "01" keeps the FE's non-deletable base row. Amounts are ex-VAT — RecalculateFromItems
                // adds VAT on top.
                if (existingBase is null)
                    fee.AddItem(AppraisalFee.BaseFeeCode, resolved.Description, amount);
                else
                    fee.UpdateItem(existingBase.Id, AppraisalFee.BaseFeeCode, resolved.Description, amount);
                wroteBase = true;

                logger.LogInformation(
                    "Appraisal fee created: fee {FeeId} base item (Kind={Kind}, Amount={Amount}) for AssignmentId={AssignmentId}",
                    fee.Id, resolved.Kind, amount, assignmentId);
            }
            else
            {
                // The current source prices nothing, so a base left by a previous source must not keep billing.
                if (existingBase is not null)
                    fee.RemoveItem(existingBase.Id);

                logger.LogInformation(
                    "No base fee resolved (Kind={Kind}) for AppraisalId={AppraisalId}, AssignmentId={AssignmentId}.",
                    resolved.Kind, appraisalId, assignmentId);
            }
        }

        // An admin may have added their own 01 while this ran (the two writes are not serialised and nothing in the
        // database forbids two 01 lines). The admin's wins: drop the system one, after reloading the items so the
        // totals include theirs. Only checked when this call wrote the base: an untouched one was judged earlier, and
        // the admin's add re-checks from its side (FeeAdminStage.EnsureSingleBaseItemAsync).
        // ponytail: narrows the window, does not close it; a filtered unique index on (AppraisalFeeId) WHERE
        // FeeCode = '01' would, and needs a migration.
        if (wroteBase && existingBase is not null)
        {
            // Null = the item is gone (the admin deleted it meanwhile); Admin = the admin overrode THIS item.
            var storedSource = await dbContext.AppraisalFeeItems.AsNoTracking()
                .Where(i => i.Id == existingBase.Id).Select(i => i.Source).FirstOrDefaultAsync(ct);

            if (storedSource is null)
            {
                // Our unsaved edit would UPDATE a row that no longer exists (a DbUpdateConcurrencyException in the
                // consumer). Drop it from the fee and from tracking instead, and bill nothing for it.
                fee.RemoveItem(existingBase.Id);
                dbContext.Entry(existingBase).State = EntityState.Detached;
                wroteBase = false;
                logger.LogWarning(
                    "AppraisalFee {FeeId}: the base item was deleted while it was re-priced; skipped the stale update.", fee.Id);
            }
            else if (storedSource == FeeItemSource.Admin)
            {
                // The admin overrode THIS item (made it their own) while we re-priced it: theirs stands. Take their
                // row back over our unsaved edit.
                await dbContext.Entry(existingBase).ReloadAsync(ct);
                fee.RecalculateFromItems();
                wroteBase = false;
                logger.LogInformation(
                    "AppraisalFee {FeeId}: the admin overrode the base item while it was re-priced; kept their override.", fee.Id);
            }
        }

        if (wroteBase
            && fee.Items.FirstOrDefault(i => i.FeeCode == AppraisalFee.BaseFeeCode && i.Source == FeeItemSource.System) is { } systemBase
            && await dbContext.AppraisalFeeItems.AsNoTracking().AnyAsync(
                // Not the item being removed: an admin who converted THAT very item (their override of it) owns it now.
                i => i.AppraisalFeeId == fee.Id && i.Id != systemBase.Id
                     && i.FeeCode == AppraisalFee.BaseFeeCode && i.Source == FeeItemSource.Admin, ct))
        {
            // A query, not Collection.LoadAsync: that is a no-op once the navigation was loaded by the Include above.
            await dbContext.AppraisalFeeItems.Where(i => i.AppraisalFeeId == fee.Id).LoadAsync(ct);
            fee.RemoveItem(systemBase.Id);
            logger.LogInformation(
                "AppraisalFee {FeeId}: an admin base item appeared concurrently; dropped the system base item.", fee.Id);
        }

        // Nothing billable on the shell at all: there is no absorb to finalise — but a removed base may have just
        // clamped the requested one to 0, so put it back for whoever bills this fee next.
        if (!fee.HasItems)
        {
            if (requestedAbsorb > 0)
                fee.RestoreRequestedBankAbsorb(requestedAbsorb);
            return;
        }

        // Step 4 — Finalise bank absorb. For payment types 05/06/07 the bank absorbs the full
        // customer bill (TotalFeeAfterVAT), overriding any user-entered AbsorbedAmount — once, at the genuine
        // assignment (repriceSystemBase) or when this call produced the first items. A replay or later route-back
        // re-fire must not re-absorb items added after assignment. For other types, re-apply whatever was
        // captured on the shell so CustomerPayableAmount is correct.
        // AddItem already called RecalculateFromItems; SetBankAbsorb re-triggers it.
        if ((repriceSystemBase || !hadItems)
            && fee.FeePaymentType is { } paymentType && FullAbsorbFeePaymentTypes.Contains(paymentType))
        {
            fee.SetBankAbsorb(fee.TotalFeeAfterVAT);
            logger.LogInformation(
                "Full-absorb applied to fee {FeeId}: FeePaymentType={FeePaymentType}, BankAbsorbAmount={BankAbsorbAmount}",
                fee.Id, paymentType, fee.BankAbsorbAmount);
        }
        else if (requestedAbsorb > 0)
        {
            // Nothing billable yet (only items awaiting approval or rejected): there is no total to cap against, so
            // keep the request for whoever bills this fee next, as the no-items branch does. Capping at 0 would lose it.
            if (fee.TotalFeeAfterVAT <= 0m)
                fee.RestoreRequestedBankAbsorb(requestedAbsorb);
            else
                // Capped at the final total only now; an admin-stage edit may have left it above a partial one.
                fee.SetBankAbsorb(Math.Min(requestedAbsorb, fee.TotalFeeAfterVAT));
        }
    }

    /// <summary>True (after logging) when the source is a quotation with no usable price.</summary>
    private bool WarnIfUnpricedQuotation(AssignmentFeeSource source, Guid assignmentId)
    {
        if (source is not AssignmentFeeSource.Quotation { Amount: not > 0m } noPrice)
            return false;

        logger.LogWarning(
            "Winning quotation {QuotationRequestId} has no usable price for AssignmentId={AssignmentId} ({PriceError}); " +
            "no base fee created. Likely the admin's own base was removed after Assign.",
            noPrice.QuotationRequestId, assignmentId, noPrice.PriceError);
        return true;
    }

    public Task<AssignmentFeeSource> ResolveSourceForAppraisalAsync(
        Domain.Appraisals.Appraisal appraisal,
        AssignmentFeeSource defaultSource,
        CancellationToken ct) =>
        ResolveSourceForAppraisalAsync(appraisal.AppraisalType, appraisal.PrevAppraisalId, defaultSource, ct);

    public async Task<AssignmentFeeSource> ResolveSourceForAppraisalAsync(
        string appraisalType,
        Guid? prevAppraisalId,
        AssignmentFeeSource defaultSource,
        CancellationToken ct)
    {
        // The quoted price outranks the chained fee: a Progressive appraisal that went to quotation is
        // billed what the winner quoted.
        if (defaultSource is AssignmentFeeSource.Quotation)
            return defaultSource;

        if (appraisalType != AppraisalTypes.Progressive || prevAppraisalId is not { } prevId)
        {
            // Stamp the appraisal type onto a tier lookup so it can pick the type-scoped ladder
            // (e.g. the flat PreAppraisal/block rate).
            return defaultSource is AssignmentFeeSource.TierBased
                ? new AssignmentFeeSource.TierBased(appraisalType)
                : defaultSource;
        }

        var ciSource = await ResolveChainedInspectionFeeAsync(prevId, ct);

        logger.LogInformation(
            "Resolved Construction Inspection fee source from PrevAppraisalId={PrevAppraisalId}: Amount={Amount}",
            prevId, ciSource.Amount);

        return ciSource;
    }

    public async Task<BaseFeeResolution> ResolveBaseFeeAsync(
        AssignmentFeeSource source,
        decimal totalSellingPrice,
        CancellationToken ct)
    {
        switch (source)
        {
            case AssignmentFeeSource.TierBased tierSource:
            {
                var appraisalType = tierSource.AppraisalType;

                var tiers = await dbContext.FeeStructures
                    .AsNoTracking()
                    .Where(fs => fs.IsActive && fs.FeeCode == AppraisalFee.BaseFeeCode
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
                        "No active fee tier (FeeCode=01) configured for AppraisalType={AppraisalType}. No base fee item will be created.",
                        appraisalType);
                    return new BaseFeeResolution(null, "", FeeBaseKinds.None, SellingPrice: totalSellingPrice);
                }

                var matched = candidates.FirstOrDefault(t => t.IsApplicableFor(totalSellingPrice));
                if (matched is null)
                {
                    matched = candidates.OrderByDescending(t => t.MinSellingPrice).First();
                    logger.LogWarning(
                        "No fee tier matched TotalSellingPrice {TotalSellingPrice} (AppraisalType={AppraisalType}). Falling back to highest tier (BaseAmount={BaseAmount})",
                        totalSellingPrice, appraisalType, matched.BaseAmount);
                }

                // A tier configured at 0 prices nothing: never a 01 at 0 (the preview and the Assign guard agree).
                if (matched.BaseAmount <= 0m)
                {
                    logger.LogWarning(
                        "Fee tier for AppraisalType={AppraisalType} matched TotalSellingPrice {TotalSellingPrice} with a zero BaseAmount. No base fee resolved.",
                        appraisalType, totalSellingPrice);
                    return new BaseFeeResolution(null, "", FeeBaseKinds.None, SellingPrice: totalSellingPrice);
                }

                return new BaseFeeResolution(
                    matched.BaseAmount,
                    await ResolveFeeNameAsync(matched.FeeCode, ct),
                    FeeBaseKinds.Tier,
                    SellingPrice: totalSellingPrice,
                    TierMin: matched.MinSellingPrice,
                    TierMax: matched.MaxSellingPrice);
            }

            case AssignmentFeeSource.Quotation quotationSource:
            {
                var rfqLabel = !string.IsNullOrWhiteSpace(quotationSource.QuotationNumber)
                    ? quotationSource.QuotationNumber
                    : quotationSource.QuotationRequestId.ToString();

                // No usable price: nothing is resolved, so a 01 at 0 can never come out of here.
                if (quotationSource.Amount is not > 0m)
                    return new BaseFeeResolution(
                        null, "", FeeBaseKinds.QuotationPending, QuotationNumber: quotationSource.QuotationNumber);

                return new BaseFeeResolution(
                    quotationSource.Amount,
                    $"Appraisal fee agreed via competitive quotation {rfqLabel}",
                    FeeBaseKinds.Quotation,
                    QuotationNumber: quotationSource.QuotationNumber);
            }

            case AssignmentFeeSource.ConstructionInspection ciSource:
            {
                // CI bypasses tier/quotation. If the prior keyed no inspection fee, add no base item
                // per spec (no fallback to tier, none to the prior's own appraisal fee).
                if (ciSource.Amount is null or <= 0m)
                    return new BaseFeeResolution(
                        null, "", FeeBaseKinds.None, PriorAppraisalNumber: ciSource.PriorAppraisalNumber);

                return new BaseFeeResolution(
                    ciSource.Amount.Value,
                    "Construction inspection fee from prior appraisal",
                    FeeBaseKinds.ConstructionInspectionFee,
                    PriorAppraisalNumber: ciSource.PriorAppraisalNumber);
            }

            default:
                logger.LogWarning("Unknown AssignmentFeeSource type {SourceType}. No base fee resolved.",
                    source.GetType().Name);
                return new BaseFeeResolution(null, "", FeeBaseKinds.None);
        }
    }

    /// <summary>
    /// The inspection fee the NEXT Progressive appraisal should charge, read from the PICKED prior
    /// appraisal's own fee rows rather than from its CollateralEngagement.
    ///
    /// Whatever the prior's type, the figure keyed on its fee page ("construction inspection fee",
    /// AppraisalFee.ConstructionInspectionFeeAmount, must be &gt; 0) is the ONLY source. The prior's own
    /// appraisal fee (FeeCode "01") never stands in for it.
    ///
    /// Returns a source with a null amount when the prior appraisal, its assignment or its fee row is
    /// missing, or no inspection fee was keyed — the caller adds no base item then, deliberately, with
    /// no fallback to the tier ladder or to the prior's appraisal fee.
    /// </summary>
    private async Task<AssignmentFeeSource.ConstructionInspection> ResolveChainedInspectionFeeAsync(
        Guid prevAppraisalId,
        CancellationToken ct)
    {
        var prior = await dbContext.Appraisals
            .AsNoTracking()
            .Where(a => a.Id == prevAppraisalId)
            .Select(a => new { a.AppraisalNumber })
            .FirstOrDefaultAsync(ct);

        if (prior is null)
        {
            logger.LogWarning(
                "Construction Inspection fee: prior appraisal {PrevAppraisalId} not found; leaving the fee empty.",
                prevAppraisalId);
            return new AssignmentFeeSource.ConstructionInspection(null);
        }

        // Same assignment election as the engagement writer: the newest live one, because a re-assigned case
        // must bill from the assignment that did the work. The global query filter already drops Rejected and
        // Cancelled rows. A missing assignment or fee row both read as "no inspection fee keyed".
        var latestAssignmentId = dbContext.AppraisalAssignments
            .AsNoTracking()
            .NewestFirstFor(prevAppraisalId)
            .Select(a => a.Id)
            .Take(1);

        var priorFee = await dbContext.AppraisalFees
            .AsNoTracking()
            .Where(f => latestAssignmentId.Contains(f.AssignmentId))
            .Select(f => f.ConstructionInspectionFeeAmount)
            .FirstOrDefaultAsync(ct);

        return new AssignmentFeeSource.ConstructionInspection(
            priorFee is > 0m ? priorFee : null, PriorAppraisalNumber: prior.AppraisalNumber);
    }
}
