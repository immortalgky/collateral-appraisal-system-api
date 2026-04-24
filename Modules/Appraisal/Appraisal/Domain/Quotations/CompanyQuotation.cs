namespace Appraisal.Domain.Quotations;

/// <summary>
/// Quotation submissions from external appraisal companies.
///
/// Status vocabulary: Submitted | UnderReview | Tentative | Negotiating | Accepted | Rejected | Withdrawn | Declined
/// IsShortlisted flag: toggled by admin while QuotationRequest is in UnderAdminReview.
/// Declined: ext-company declines the whole invitation (no bid submitted, or bid withdrawn-and-declined).
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
    public string Status { get; private set; } = null!;
    public bool IsWinner { get; private set; }

    // ── Shortlist + negotiation fields ───────────────────────────────────────
    /// <summary>Admin flag. True when this quotation is on the shortlist for RM review.</summary>
    public bool IsShortlisted { get; private set; }

    /// <summary>Snapshot of TotalQuotedPrice at the moment admin first proposes a negotiated price.</summary>
    public decimal? OriginalQuotedPrice { get; private set; }

    /// <summary>Latest agreed/proposed price after negotiation rounds.</summary>
    public decimal? CurrentNegotiatedPrice { get; private set; }

    /// <summary>Number of negotiation rounds opened for this quotation. Max is MaxNegotiationRounds.</summary>
    public int NegotiationRounds { get; private set; }

    public const int MaxNegotiationRounds = 3;

    // Company Contact
    public string? SubmittedByName { get; private set; }
    public string? SubmittedByEmail { get; private set; }
    public string? SubmittedByPhone { get; private set; }

    // ── Decline fields ────────────────────────────────────────────────────────
    public string? DeclineReason { get; private set; }
    public DateTime? DeclinedAt { get; private set; }
    public string? DeclinedBy { get; private set; }

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
            //Id = Guid.CreateVersion7(),
            QuotationRequestId = quotationRequestId,
            InvitationId = invitationId,
            CompanyId = companyId,
            QuotationNumber = quotationNumber,
            SubmittedAt = DateTime.UtcNow,
            EstimatedDays = estimatedDays,
            Status = "Submitted",
            TotalQuotedPrice = 0,
            IsShortlisted = false,
            NegotiationRounds = 0
        };
    }

    /// <summary>
    /// Creates a new draft quotation (Maker workflow). Status = "Draft".
    /// Items are added via AddItem as usual; invitation is NOT marked submitted until the final submit.
    /// </summary>
    public static CompanyQuotation CreateDraft(
        Guid quotationRequestId,
        Guid invitationId,
        Guid companyId,
        string quotationNumber,
        int estimatedDays)
    {
        var q = new CompanyQuotation
        {
            Id = Guid.CreateVersion7(),
            QuotationRequestId = quotationRequestId,
            InvitationId = invitationId,
            CompanyId = companyId,
            QuotationNumber = quotationNumber,
            SubmittedAt = default,
            EstimatedDays = estimatedDays,
            Status = "Draft",
            TotalQuotedPrice = 0,
            Currency = "THB"
        };
        return q;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Item management
    // ─────────────────────────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────────────────────
    // Maker / Checker flow transitions
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Maker promotes a draft to Checker review. Draft → PendingCheckerReview.
    /// </summary>
    public void MarkPendingCheckerReview()
    {
        if (Status != "Draft")
            throw new InvalidOperationException($"Cannot submit to checker: status is '{Status}', expected 'Draft'");
        Status = "PendingCheckerReview";
    }

    /// <summary>
    /// Checker finalises the submission. Draft or PendingCheckerReview → Submitted.
    /// Stamps SubmittedAt to the current UTC time.
    /// </summary>
    public void MarkSubmitted()
    {
        if (Status != "Draft" && Status != "PendingCheckerReview")
            throw new InvalidOperationException(
                $"Cannot submit: status is '{Status}', expected 'Draft' or 'PendingCheckerReview'");
        Status = "Submitted";
        SubmittedAt = DateTime.UtcNow;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Legacy status transitions (kept for non-IBG path)
    // ─────────────────────────────────────────────────────────────────────────

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

    /// <summary>
    /// Ext-company declines the invitation — either before submitting or after having submitted.
    /// Terminal state for this company's participation.
    /// Emitting a domain event happens at the aggregate root level (QuotationRequest) —
    /// or the caller is responsible for triggering MarkAllResponsesReceived.
    /// </summary>
    public void Decline(string reason, string declinedBy)
    {
        var terminalStatuses = new[] { "Accepted", "Withdrawn", "Declined" };
        if (terminalStatuses.Contains(Status))
            throw new InvalidOperationException($"Cannot decline: quotation is already in terminal status '{Status}'.");

        DeclineReason = reason;
        DeclinedAt = DateTime.UtcNow;
        DeclinedBy = declinedBy;
        Status = "Declined";
    }

    /// <summary>
    /// Creates a <see cref="CompanyQuotation"/> representing a company that declined the invitation
    /// without ever submitting a bid.
    /// </summary>
    public static CompanyQuotation CreateDeclined(
        Guid quotationRequestId,
        Guid invitationId,
        Guid companyId,
        string quotationNumber,
        string reason,
        string declinedBy)
    {
        return new CompanyQuotation
        {
            QuotationRequestId = quotationRequestId,
            InvitationId = invitationId,
            CompanyId = companyId,
            QuotationNumber = quotationNumber,
            SubmittedAt = DateTime.UtcNow,
            EstimatedDays = 0,
            Status = "Declined",
            TotalQuotedPrice = 0,
            IsShortlisted = false,
            NegotiationRounds = 0,
            DeclineReason = reason,
            DeclinedAt = DateTime.UtcNow,
            DeclinedBy = declinedBy
        };
    }

    public void MarkAsWinner()
    {
        IsWinner = true;
        Status = "Accepted";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IBG shortlist + negotiation methods
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Admin adds this quotation to the shortlist.</summary>
    public void MarkShortlisted()
    {
        IsShortlisted = true;
        if (Status == "Submitted") Status = "UnderReview";
    }

    /// <summary>Admin removes this quotation from the shortlist.</summary>
    public void ClearShortlisted()
    {
        IsShortlisted = false;
        if (Status == "UnderReview") Status = "Submitted";
    }

    /// <summary>Marks this quotation as the tentative winner.</summary>
    public void MarkTentative()
    {
        if (Status != "Submitted" && Status != "UnderReview")
            throw new InvalidOperationException($"Cannot mark quotation as tentative in status '{Status}'");

        Status = "Tentative";
    }

    /// <summary>Clears tentative status (e.g., when replacing winner or on reject).</summary>
    public void ClearTentative()
    {
        if (Status == "Tentative" || Status == "Negotiating")
            Status = IsShortlisted ? "UnderReview" : "Submitted";
    }

    /// <summary>
    /// Admin opens a negotiation round with a proposed price.
    /// Guards: max rounds, correct status.
    /// </summary>
    public QuotationNegotiation OpenNegotiationRound(decimal proposedPrice, Guid adminUserId, string message)
    {
        if (Status != "Tentative" && Status != "Negotiating")
            throw new InvalidOperationException($"Cannot open negotiation round in status '{Status}'");

        if (NegotiationRounds >= MaxNegotiationRounds)
            throw new InvalidOperationException(
                $"Maximum negotiation rounds ({MaxNegotiationRounds}) reached for this quotation");

        // Use QuotationRequestId as a placeholder quotation item id — the negotiation is at quotation level, not per-item
        // (QuotationNegotiation.QuotationItemId is kept for legacy, we use the quotation's own Id)
        var negotiation = QuotationNegotiation.Create(
            Id,
            Id, // quotation-level negotiation; reuse own Id as sentinel
            NegotiationRounds + 1,
            "Admin",
            adminUserId,
            message,
            proposedPrice);

        _negotiations.Add(negotiation);
        NegotiationRounds++;
        UpdateNegotiatedPrice(proposedPrice);
        Status = "Negotiating";

        return negotiation;
    }

    /// <summary>
    /// Ext-company responds to an open negotiation round.
    /// verb: Accept | Reject | Counter
    /// </summary>
    public void RespondNegotiation(Guid negotiationId, string verb, decimal? counterPrice, string? message,
        Guid companyUserId)
    {
        var negotiation = _negotiations.FirstOrDefault(n => n.Id == negotiationId)
                          ?? throw new InvalidOperationException($"Negotiation '{negotiationId}' not found");

        if (negotiation.Status != "Pending")
            throw new InvalidOperationException(
                $"Negotiation '{negotiationId}' is no longer pending (status: {negotiation.Status})");

        switch (verb)
        {
            case "Accept":
                negotiation.Accept(companyUserId, message);
                // Return to Tentative — the parent QuotationRequest transitions to WinnerTentative.
                // CompanyQuotation.Status reflects the company's acceptance by reverting to Tentative
                // so the status chain is consistent: Tentative → (admin opens) → Negotiating → (company accepts) → Tentative.
                Status = "Tentative";
                break;

            case "Counter":
                if (!counterPrice.HasValue)
                    throw new InvalidOperationException("Counter response requires a counter price");
                negotiation.Counter(companyUserId, message ?? string.Empty);
                UpdateNegotiatedPrice(counterPrice.Value);
                break;

            case "Reject":
                negotiation.Reject(companyUserId, message);
                Withdraw();
                break;

            default:
                throw new ArgumentException($"Unknown negotiation verb '{verb}'. Use Accept, Counter, or Reject");
        }
    }

    /// <summary>
    /// Updates the negotiated price. On first call, snapshots the original quoted price.
    /// </summary>
    public void UpdateNegotiatedPrice(decimal price)
    {
        if (!OriginalQuotedPrice.HasValue)
            OriginalQuotedPrice = TotalQuotedPrice;

        CurrentNegotiatedPrice = price;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Internal helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void RecalculateTotalPrice()
    {
        // If any item has a fee breakdown entered, use the derived NetAmount.
        // Legacy rows (FeeAmount == 0) fall back to their stored QuotedPrice.
        TotalQuotedPrice = _items.Sum(i => i.FeeAmount > 0 ? i.NetAmount : i.QuotedPrice);
    }

    /// <summary>
    /// Removes all items from this quotation. Used by the SaveDraft replace-all strategy.
    /// EF Core change-tracking will issue DELETE statements for each removed child on SaveChanges.
    /// </summary>
    internal void ClearItems()
    {
        _items.Clear();
    }

    internal void AddNegotiation(QuotationNegotiation negotiation)
    {
        _negotiations.Add(negotiation);
    }
}