using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals.Exceptions;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Quotations;
using Appraisal.Infrastructure.Repositories;
using Auth.Contracts.Companies;

namespace Appraisal.Application.Features.Fees.GetFeePreview;

/// <summary>
/// The fee assignment WOULD create, without creating it. The base amount comes from the same
/// <see cref="IAssignmentFeeService"/> resolution that <c>EnsureAssignmentFeeItemsAsync</c> materialises, so the
/// preview cannot drift from the real result; only the quotation lookup is repeated here, because assignment
/// knows the winning company and the preview may not.
/// </summary>
public class GetFeePreviewQueryHandler(
    AppraisalDbContext dbContext,
    IAssignmentFeeService feeService,
    ICompanyLookupService companyLookup) : IQueryHandler<GetFeePreviewQuery, FeePreviewResult>
{
    public async Task<FeePreviewResult> Handle(GetFeePreviewQuery query, CancellationToken cancellationToken)
    {
        var appraisal = await dbContext.Appraisals
            .AsNoTracking()
            .Where(a => a.Id == query.AppraisalId)
            .Select(a => new { a.AppraisalType, a.PrevAppraisalId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new AppraisalNotFoundException(query.AppraisalId);

        // The same current-assignment election the admin-stage fee writes use.
        var active = await FeeAdminStage.CurrentAssignment(dbContext, query.AppraisalId)
            .Select(a => new { a.Id, a.AssignmentType, a.AssignmentMethod, a.AssigneeCompanyId, a.AssignmentStatus })
            .FirstOrDefaultAsync(cancellationToken);

        var explicitMethod = NormalizeMethod(query.AssignmentMethod);
        var type = NormalizeType(query.AssignmentType)
                   // Asking about a quotation only makes sense for an external company.
                   ?? (explicitMethod == "quotation" ? "external"
                       : active?.AssignmentMethod == AppraisalAssignment.OfflineAssignmentMethod
                           ? "external-offline"
                           : active?.AssignmentType.Code == AssignmentType.External.Code
                               ? "external"
                               : "internal");
        var method = explicitMethod
                     ?? (string.Equals(active?.AssignmentMethod, "Quotation", StringComparison.OrdinalIgnoreCase)
                         ? "quotation"
                         : "manual");
        // Only an external in-system assignment can be priced by quotation; internal and off-system engagements
        // go through the tier ladder whatever method the screen shows.
        var isQuotation = method == "quotation" && type == "external";
        var companyId = query.CompanyId
                        ?? (query.AssignmentMethod is null && Guid.TryParse(active?.AssigneeCompanyId, out var assigned)
                            ? assigned
                            : (Guid?)null);

        var fee = await LoadFeeAsync(query.AppraisalId, active?.Id, cancellationToken);
        if (fee is null)
        {
            // Assignment skips fee materialisation when the appraisal has no fee row, so nothing would be charged.
            return Build(null, null, new FeeBaseSourceDto(FeeBaseKinds.None, null, null, null, null, null, null),
                existingItems: [], vatRate: AppraisalFee.DefaultVatRate);
        }

        BaseFeeResolution resolved;
        Guid? winnerId = null;
        if (fee.Blank)
        {
            // The row an admin's first edit created for a request without fee data: assignment bills no base for it,
            // whatever the source (AssignmentFeeService), so there is nothing to show.
            resolved = new BaseFeeResolution(null, "", FeeBaseKinds.None);
        }
        else if (isQuotation)
        {
            (resolved, winnerId) = await ResolveQuotationAsync(
                query.AppraisalId, companyId, fee.TotalSellingPrice ?? 0m, cancellationToken);
        }
        else
        {
            var source = await feeService.ResolveSourceForAppraisalAsync(
                appraisal.AppraisalType, appraisal.PrevAppraisalId, new AssignmentFeeSource.TierBased(), cancellationToken);
            resolved = await feeService.ResolveBaseFeeAsync(source, fee.TotalSellingPrice ?? 0m, cancellationToken);
        }

        // Only now, when the name is about to be shown: every path that returned earlier had no winner to name.
        var winnerName = winnerId is { } winner
            ? (await companyLookup.GetByIdAsync(winner, cancellationToken))?.Name
            : null;

        var baseSource = new FeeBaseSourceDto(
            resolved.Kind,
            resolved.PriorAppraisalNumber,
            resolved.QuotationNumber,
            winnerName,
            resolved.SellingPrice,
            resolved.TierMin,
            resolved.TierMax);

        // Assignment re-prices an existing system base only for a row still awaiting assignment (a route-back).
        return Build(fee.Id, resolved, baseSource, fee.Items, fee.VATRate,
            repriceSystemBase: active?.AssignmentStatus == AssignmentStatus.Pending);
    }

    // The fee of the current assignment, with its items, in one projected query; failing that, the newest fee on the
    // appraisal (the same rule a fresh shell copies its context from), whose selling price and VAT a fresh shell
    // would take (AppraisalFee.Create gives it the default VAT rate; only the context fields are copied). Its items
    // are NOT carried over — a new shell starts empty.
    private async Task<FeeRow?> LoadFeeAsync(Guid appraisalId, Guid? activeAssignmentId, CancellationToken ct)
    {
        if (activeAssignmentId is { } id)
        {
            var current = await dbContext.AppraisalFees
                .AsNoTracking()
                .Where(f => f.AssignmentId == id)
                .OrderBy(f => f.Id) // unique, so the collection projection cannot pair the row with another's items
                .Select(f => new FeeRow(
                    f.Id,
                    f.TotalSellingPrice,
                    f.VATRate,
                    f.Items
                        .Select(i => new ItemRow(i.Id, i.FeeCode, i.FeeDescription, i.FeeAmount, i.Source,
                            AppraisalFeeItem.IsBillableState(i.RequiresApproval, i.ApprovalStatus)))
                        .ToList(),
                    AppraisalFee.IsBlankShell(f.FeePaymentType, f.FeeNotes, f.TotalSellingPrice)))
                .FirstOrDefaultAsync(ct);

            if (current is not null)
                return current;
        }

        var latest = await FeeShellFactory.LatestFee(dbContext, appraisalId, activeAssignmentId)
            .AsNoTracking()
            .Select(f => new { f.TotalSellingPrice, f.FeePaymentType, f.FeeNotes })
            .FirstOrDefaultAsync(ct);

        return latest is null
            ? null
            : new FeeRow(null, latest.TotalSellingPrice, AppraisalFee.DefaultVatRate, [],
                AppraisalFee.IsBlankShell(latest.FeePaymentType, latest.FeeNotes, latest.TotalSellingPrice));
    }

    private async Task<(BaseFeeResolution Resolved, Guid? WinnerId)> ResolveQuotationAsync(
        Guid appraisalId,
        Guid? companyId,
        decimal totalSellingPrice,
        CancellationToken ct)
    {
        var pending = new BaseFeeResolution(null, "", FeeBaseKinds.QuotationPending);

        // Just the winner and the price it quoted for this appraisal, from the same "latest finalized" election the
        // assignment consumer uses (QuotationQueries.LatestFinalized); no aggregate is loaded.
        var rfq = await dbContext.QuotationRequests
            .AsNoTracking()
            .LatestFinalized(appraisalId)
            .Select(q => new
            {
                q.Id,
                q.QuotationNumber,
                q.SelectedCompanyId,
                Prices = q.Quotations
                    .Where(c => c.IsWinner && c.CompanyId == q.SelectedCompanyId)
                    .SelectMany(c => c.Items)
                    .Where(i => i.AppraisalId == appraisalId)
                    .OrderBy(i => i.ItemNumber).ThenBy(i => i.Id) // the item QuotationFeePrice.Resolve takes first
                    .AsQueryable()
                    .Select(CompanyQuotationItem.ExVatFeeExpression)
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (rfq?.SelectedCompanyId is not { } winnerId)
            return (pending, null);

        pending = pending with { QuotationNumber = rfq.QuotationNumber };

        // The current round decides who is billed: a different company would not get this price.
        if (companyId is { } chosen && chosen != winnerId)
            return (pending, winnerId);

        // The winner's first item for this appraisal, priced by the same rule the assignment consumer applies.
        var price = rfq.Prices.Count == 0 ? null : QuotationFeePrice.FromExVatFee(rfq.Prices[0], appraisalId);
        if (price?.Amount is not { } amount)
            return (pending, winnerId);

        // A quotation price outranks the chained fee for every type, so there is no source election to repeat here.
        return (await feeService.ResolveBaseFeeAsync(
            new AssignmentFeeSource.Quotation(amount, rfq.Id, rfq.QuotationNumber), totalSellingPrice, ct), winnerId);
    }

    private static FeePreviewResult Build(
        Guid? feeId,
        BaseFeeResolution? resolved,
        FeeBaseSourceDto baseSource,
        IReadOnlyList<ItemRow> existingItems,
        decimal vatRate,
        bool repriceSystemBase = false)
    {
        // What assignment would do to a system base left by an earlier source (route-back to Pending): replace it
        // with the current source's, or drop it when that prices nothing. An admin's 01 always wins and is kept.
        // The would-be line keeps the stored system item's id: the admin's "Edit amount" then updates that item (the
        // server turns it into the admin's base) instead of posting a second 01.
        var hadItems = existingItems.Count > 0;
        Guid? replacedSystemBaseId = null;
        if (repriceSystemBase)
        {
            replacedSystemBaseId = existingItems
                .FirstOrDefault(i => i.FeeCode == AppraisalFee.BaseFeeCode && i.Source == FeeItemSource.System)?.Id;
            existingItems = existingItems
                .Where(i => !(i.FeeCode == AppraisalFee.BaseFeeCode && i.Source == FeeItemSource.System))
                .ToList();
        }

        var items = existingItems
            .Select(i => new FeePreviewItemDto(i.Id, i.FeeCode, i.Description, i.Amount, i.Source))
            .ToList();
        var billable = existingItems.Where(i => i.Billable).Sum(i => i.Amount);

        var adminOverride = existingItems
            .Where(i => i.FeeCode == AppraisalFee.BaseFeeCode && i.Source == FeeItemSource.Admin)
            .Select(i => (decimal?)i.Amount)
            .FirstOrDefault();

        var hasBillableBase = existingItems.Any(i => IsBillableBase(i.FeeCode, i.Amount, i.Source, i.Billable));

        // The would-be system base item appears only when nothing (system or admin) already holds the 01 line.
        // Assignment only adds a base on a genuine (re)assignment or to a fee with no items at all (a re-fire must not
        // add one to a fee that already holds items, AssignmentFeeService), so a non-Pending row with items shows none.
        if (resolved?.Amount is { } systemAmount && existingItems.All(i => i.FeeCode != AppraisalFee.BaseFeeCode)
            && (repriceSystemBase || !hadItems))
        {
            items.Add(new FeePreviewItemDto(replacedSystemBaseId, AppraisalFee.BaseFeeCode, resolved.Description, systemAmount,
                FeeItemSource.System));
            billable += systemAmount;
            hasBillableBase |= IsBillableBase(AppraisalFee.BaseFeeCode, systemAmount, FeeItemSource.System, true);
        }

        // The very arithmetic of AppraisalFee.RecalculateFromItems (shared helper, same rounding).
        var vat = AppraisalFee.CalculateVat(billable, vatRate);

        return new FeePreviewResult(feeId, resolved?.Amount, baseSource, adminOverride, items, hasBillableBase, billable, vatRate, vat, billable + vat);
    }

    /// <summary>
    /// THE rule for "this line is a billable base": a 01 above 0 that is the Admin's or the System's, or a User one
    /// that is billable (not awaiting approval, not rejected — the same predicate the fee totals use). Assign
    /// (FeeBaseMissing) and the screen both read the result through <see cref="FeePreviewResult.HasBillableBase"/>.
    /// </summary>
    private static bool IsBillableBase(string feeCode, decimal amount, string source, bool billable) =>
        feeCode == AppraisalFee.BaseFeeCode && amount > 0m && (source != FeeItemSource.User || billable);

    private static string? NormalizeType(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" => null,
        "internal" => "internal",
        "external" => "external",
        "external-offline" => "external-offline",
        _ => throw new BadRequestException(
            $"assignmentType must be internal, external or external-offline (got '{value}').")
    };

    private static string? NormalizeMethod(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" => null,
        "manual" => "manual",
        "roundrobin" => "roundrobin",
        "quotation" => "quotation",
        _ => throw new BadRequestException(
            $"assignmentMethod must be manual, roundrobin or quotation (got '{value}').")
    };

    private sealed record ItemRow(Guid Id, string FeeCode, string Description, decimal Amount, string Source, bool Billable);

    private sealed record FeeRow(
        Guid? Id, decimal? TotalSellingPrice, decimal VATRate, IReadOnlyList<ItemRow> Items, bool Blank);
}
