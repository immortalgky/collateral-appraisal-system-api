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
            Id = Guid.NewGuid(),
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
}