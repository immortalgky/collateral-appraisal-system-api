using System.ComponentModel.DataAnnotations.Schema;

namespace Appraisal.Domain.Quotations;

/// <summary>
/// Itemized pricing per appraisal with negotiation tracking.
/// </summary>
public class CompanyQuotationItem : Entity<Guid>
{
    public Guid CompanyQuotationId { get; private set; }
    public Guid QuotationRequestItemId { get; private set; }
    public Guid AppraisalId { get; private set; }

    // Item Identification
    public int ItemNumber { get; private set; }

    // Pricing for this specific appraisal
    public decimal QuotedPrice { get; private set; }
    public string Currency { get; private set; } = "THB";
    public string? PriceBreakdown { get; private set; }

    // Timeline for this specific appraisal
    public int EstimatedDays { get; private set; }
    public DateTime? ProposedCompletionDate { get; private set; }

    // Item Notes
    public string? ItemNotes { get; private set; }

    // Negotiation Tracking
    public decimal OriginalQuotedPrice { get; private set; }
    public decimal CurrentNegotiatedPrice { get; private set; }
    public int NegotiationRounds { get; private set; }

    // ── Fee breakdown (Maker/Checker flow) ────────────────────────────────────
    /// <summary>Gross fee the company quotes for this appraisal.</summary>
    public decimal FeeAmount { get; private set; } = 0;

    /// <summary>First-round discount offered by the company.</summary>
    public decimal Discount { get; private set; } = 0;

    /// <summary>Second-round discount offered during negotiation. Null until negotiation occurs.</summary>
    public decimal? NegotiatedDiscount { get; private set; }

    /// <summary>VAT rate (%) applied to the after-discount amount. Defaults to 0; UI defaults to 7 on new rows.</summary>
    public decimal VatPercent { get; private set; } = 0;

    // ── Derived (non-persisted) helpers ───────────────────────────────────────
    [NotMapped]
    public decimal FeeAfterDiscount => FeeAmount - Discount - (NegotiatedDiscount ?? 0);

    [NotMapped]
    public decimal VatAmount => Math.Round(FeeAfterDiscount * VatPercent / 100m, 2, MidpointRounding.AwayFromZero);

    [NotMapped]
    public decimal NetAmount => FeeAfterDiscount + VatAmount;

    private CompanyQuotationItem()
    {
    }

    public static CompanyQuotationItem Create(
        Guid companyQuotationId,
        Guid quotationRequestItemId,
        Guid appraisalId,
        int itemNumber,
        decimal quotedPrice,
        int estimatedDays)
    {
        return new CompanyQuotationItem
        {
            //Id = Guid.CreateVersion7(),
            CompanyQuotationId = companyQuotationId,
            QuotationRequestItemId = quotationRequestItemId,
            AppraisalId = appraisalId,
            ItemNumber = itemNumber,
            QuotedPrice = quotedPrice,
            OriginalQuotedPrice = quotedPrice,
            CurrentNegotiatedPrice = quotedPrice,
            EstimatedDays = estimatedDays,
            NegotiationRounds = 0
        };
    }

    public void SetPriceBreakdown(string? breakdown)
    {
        PriceBreakdown = breakdown;
    }

    public void SetProposedCompletionDate(DateTime? date)
    {
        ProposedCompletionDate = date;
    }

    public void SetItemNotes(string? notes)
    {
        ItemNotes = notes;
    }

    public void UpdateNegotiatedPrice(decimal newPrice)
    {
        CurrentNegotiatedPrice = newPrice;
        NegotiationRounds++;
    }

    public void AcceptNegotiatedPrice()
    {
        QuotedPrice = CurrentNegotiatedPrice;
    }

    /// <summary>
    /// Sets the fee breakdown fields for this item (Maker entry).
    /// </summary>
    public void SetFeeBreakdown(decimal feeAmount, decimal discount, decimal vatPercent)
    {
        if (feeAmount < 0) throw new InvalidOperationException("FeeAmount cannot be negative");
        if (discount < 0) throw new InvalidOperationException("Discount cannot be negative");
        if (vatPercent < 0) throw new InvalidOperationException("VatPercent cannot be negative");
        if (discount > feeAmount) throw new InvalidOperationException("Discount cannot exceed FeeAmount");

        FeeAmount = feeAmount;
        Discount = discount;
        VatPercent = vatPercent;
    }

    /// <summary>
    /// Sets the negotiated discount for this item (applied during a negotiation round).
    /// </summary>
    public void SetNegotiatedDiscount(decimal? amount)
    {
        if (amount is < 0) throw new InvalidOperationException("NegotiatedDiscount cannot be negative");
        if (amount is not null && amount > FeeAmount - Discount)
            throw new InvalidOperationException("NegotiatedDiscount cannot exceed FeeAmount − Discount");

        NegotiatedDiscount = amount;
    }
}