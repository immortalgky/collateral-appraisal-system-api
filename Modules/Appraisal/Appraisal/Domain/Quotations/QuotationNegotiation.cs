namespace Appraisal.Domain.Quotations;

/// <summary>
/// Tracks counter-offers and negotiations between admin and companies.
/// </summary>
public class QuotationNegotiation : Entity<Guid>
{
    public Guid CompanyQuotationId { get; private set; }
    public Guid QuotationItemId { get; private set; }

    // Negotiation Details
    public int NegotiationRound { get; private set; }
    public string InitiatedBy { get; private set; } = null!; // Admin, Company
    public Guid? InitiatedByUserId { get; private set; }
    public DateTime InitiatedAt { get; private set; }

    // Counter Offer
    public decimal? CounterPrice { get; private set; }
    public int? CounterTimeline { get; private set; }

    // Message/Notes
    public string Message { get; private set; } = null!;

    // Response
    public string? ResponseMessage { get; private set; }
    public DateTime? RespondedAt { get; private set; }
    public Guid? RespondedBy { get; private set; }

    // Status
    public string Status { get; private set; } = null!; // Pending, Accepted, Rejected, Countered

    private QuotationNegotiation()
    {
    }

    public static QuotationNegotiation Create(
        Guid companyQuotationId,
        Guid quotationItemId,
        int negotiationRound,
        string initiatedBy,
        Guid? initiatedByUserId,
        string message,
        decimal? counterPrice = null,
        int? counterTimeline = null)
    {
        if (initiatedBy != "Admin" && initiatedBy != "Company")
            throw new ArgumentException("InitiatedBy must be 'Admin' or 'Company'");

        return new QuotationNegotiation
        {
            Id = Guid.NewGuid(),
            CompanyQuotationId = companyQuotationId,
            QuotationItemId = quotationItemId,
            NegotiationRound = negotiationRound,
            InitiatedBy = initiatedBy,
            InitiatedByUserId = initiatedByUserId,
            InitiatedAt = DateTime.UtcNow,
            Message = message,
            CounterPrice = counterPrice,
            CounterTimeline = counterTimeline,
            Status = "Pending"
        };
    }

    public void Accept(Guid respondedBy, string? responseMessage = null)
    {
        if (Status != "Pending")
            throw new InvalidOperationException($"Cannot accept negotiation in status '{Status}'");

        Status = "Accepted";
        RespondedBy = respondedBy;
        RespondedAt = DateTime.UtcNow;
        ResponseMessage = responseMessage;
    }

    public void Reject(Guid respondedBy, string? responseMessage = null)
    {
        if (Status != "Pending")
            throw new InvalidOperationException($"Cannot reject negotiation in status '{Status}'");

        Status = "Rejected";
        RespondedBy = respondedBy;
        RespondedAt = DateTime.UtcNow;
        ResponseMessage = responseMessage;
    }

    public void Counter(Guid respondedBy, string responseMessage)
    {
        if (Status != "Pending")
            throw new InvalidOperationException($"Cannot counter negotiation in status '{Status}'");

        Status = "Countered";
        RespondedBy = respondedBy;
        RespondedAt = DateTime.UtcNow;
        ResponseMessage = responseMessage;
    }
}