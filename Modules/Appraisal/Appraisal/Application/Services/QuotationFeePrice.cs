using System.Data.SqlTypes;
using Appraisal.Domain.Quotations;

namespace Appraisal.Application.Services;

/// <summary>
/// The ex-VAT fee a finalized quotation prices one appraisal at, for one winning company. The one rule shared by
/// the assignment consumer (which bills it) and the fee preview (which shows it), so they cannot disagree.
/// </summary>
internal static class QuotationFeePrice
{
    /// <summary>Exactly one of <paramref name="Amount"/> / <paramref name="Error"/> is set.</summary>
    public sealed record Result(decimal? Amount, string? Error);

    public static Result Resolve(QuotationRequest quotationRequest, Guid appraisalId, Guid companyId)
    {
        var winningQuotation = quotationRequest.Quotations.FirstOrDefault(q => q.IsWinner && q.CompanyId == companyId);
        if (winningQuotation is null)
            return new Result(null,
                $"Cannot use Quotation fee source: RFQ '{quotationRequest.QuotationNumber ?? quotationRequest.Id.ToString()}' has no winning company quotation.");

        // No per-appraisal item means the winner never priced this appraisal — the aggregate total sums other
        // appraisals (and is VAT-inclusive), so reject instead.
        // The first by ItemNumber (then Id in SQL Server's uniqueidentifier order, not .NET's Guid order), the same
        // item the SQL paths (fee preview, winner check, priced-appraisal list) take, so they all agree.
        var item = winningQuotation.Items
            .Where(i => i.AppraisalId == appraisalId)
            .OrderBy(i => i.ItemNumber).ThenBy(i => new SqlGuid(i.Id))
            .FirstOrDefault();
        if (item is null)
            return new Result(null,
                $"Cannot use Quotation fee source: winning quotation has no price for appraisal '{appraisalId}'.");

        return FromExVatFee(item.ExVatFee, appraisalId);
    }

    /// <summary>
    /// The rule on an already-computed <see cref="CompanyQuotationItem.ExVatFeeExpression"/> figure, for callers that
    /// read it through a projection instead of the aggregate (the fee preview).
    /// </summary>
    public static Result FromExVatFee(decimal exVatFee, Guid appraisalId) =>
        exVatFee <= 0m
            ? new Result(null,
                $"Cannot use Quotation fee source: winning quotation has no usable price for appraisal '{appraisalId}'.")
            : new Result(exVatFee, null);
}
