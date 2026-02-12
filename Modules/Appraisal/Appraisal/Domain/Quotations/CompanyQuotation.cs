namespace Appraisal.Domain.Quotations;

/// <summary>
/// Quotation submissions from external appraisal companies.
/// </summary>
public class CompanyQuotation : Entity<Guid>
{
    private readonly List<CompanyQuotationItem> _items = [];
    private readonly List<QuotationNegotiation> _negotiations = [];

    public IReadOnlyList<CompanyQuotationItem> Items => _items.AsReadOnly();
    public IReadOnlyList<QuotationNegotiation> Negotiations => _negotiations.AsReadOnly();

    public Guid QuotationRequestId { get; private set; }
    public Guid InvitationId { get; private set; }
    public Guid CompanyId { get; private set; }

    // Quotation Details
    public string QuotationNumber { get; private set; } = null!;
    public DateTime SubmittedAt { get; private set; }
    public DateTime? ValidUntil { get; private set; }

    // Total Pricing (calculated from items)
    public decimal TotalQuotedPrice { get; private set; }
    public string Currency { get; private set; } = "THB";

    // Overall Timeline
    public int EstimatedDays { get; private set; }
    public DateTime? ProposedStartDate { get; private set; }
    public DateTime? ProposedCompletionDate { get; private set; }

    // Additional Information
    public string? Remarks { get; private set; }
    public string? TermsAndConditions { get; private set; }

    // Status
    public string Status { get; private set; } = null!; // Submitted, UnderReview, Accepted, Rejected, Withdrawn
    public bool IsWinner { get; private set; }

    // Company Contact
    public string? SubmittedByName { get; private set; }
    public string? SubmittedByEmail { get; private set; }
    public string? SubmittedByPhone { get; private set; }

    private CompanyQuotation()
    {
    }

    public static CompanyQuotation Create(
        Guid quotationRequestId,
        Guid invitationId,
        Guid companyId,
        string quotationNumber,
        int estimatedDays)
    {
        return new CompanyQuotation
        {
            Id = Guid.CreateVersion7(),
            QuotationRequestId = quotationRequestId,
            InvitationId = invitationId,
            CompanyId = companyId,
            QuotationNumber = quotationNumber,
            SubmittedAt = DateTime.UtcNow,
            EstimatedDays = estimatedDays,
            Status = "Submitted",
            TotalQuotedPrice = 0
        };
    }

    public CompanyQuotationItem AddItem(
        Guid quotationRequestItemId,
        Guid appraisalId,
        int itemNumber,
        decimal quotedPrice,
        int estimatedDays)
    {
        var item = CompanyQuotationItem.Create(
            Id, quotationRequestItemId, appraisalId, itemNumber, quotedPrice, estimatedDays);
        _items.Add(item);
        RecalculateTotalPrice();
        return item;
    }

    public void SetValidUntil(DateTime validUntil)
    {
        ValidUntil = validUntil;
    }

    public void SetProposedDates(DateTime? startDate, DateTime? completionDate)
    {
        ProposedStartDate = startDate;
        ProposedCompletionDate = completionDate;
    }

    public void SetRemarks(string? remarks)
    {
        Remarks = remarks;
    }

    public void SetTermsAndConditions(string? terms)
    {
        TermsAndConditions = terms;
    }

    public void SetContactInfo(string? name, string? email, string? phone)
    {
        SubmittedByName = name;
        SubmittedByEmail = email;
        SubmittedByPhone = phone;
    }

    public void MarkUnderReview()
    {
        if (Status != "Submitted")
            throw new InvalidOperationException($"Cannot review quotation in status '{Status}'");

        Status = "UnderReview";
    }

    public void Accept()
    {
        if (Status != "UnderReview" && Status != "Submitted")
            throw new InvalidOperationException($"Cannot accept quotation in status '{Status}'");

        Status = "Accepted";
    }

    public void Reject()
    {
        if (Status == "Accepted" || Status == "Withdrawn")
            throw new InvalidOperationException($"Cannot reject quotation in status '{Status}'");

        Status = "Rejected";
    }

    public void Withdraw()
    {
        if (Status == "Accepted")
            throw new InvalidOperationException("Cannot withdraw an accepted quotation");

        Status = "Withdrawn";
    }

    public void MarkAsWinner()
    {
        IsWinner = true;
        Status = "Accepted";
    }

    private void RecalculateTotalPrice()
    {
        TotalQuotedPrice = _items.Sum(i => i.QuotedPrice);
    }

    internal void AddNegotiation(QuotationNegotiation negotiation)
    {
        _negotiations.Add(negotiation);
    }
}