namespace Appraisal.Domain.Quotations;

/// <summary>
/// QuotationRequest Aggregate Root.
/// Manages Request for Quotation (RFQ) process for external appraisal assignments.
/// </summary>
public class QuotationRequest : Aggregate<Guid>
{
    private readonly List<QuotationRequestItem> _items = [];
    private readonly List<QuotationInvitation> _invitations = [];
    private readonly List<CompanyQuotation> _quotations = [];

    public IReadOnlyList<QuotationRequestItem> Items => _items.AsReadOnly();
    public IReadOnlyList<QuotationInvitation> Invitations => _invitations.AsReadOnly();
    public IReadOnlyList<CompanyQuotation> Quotations => _quotations.AsReadOnly();

    // RFQ Information
    public string QuotationNumber { get; private set; } = null!;
    public DateTime RequestDate { get; private set; }
    public DateTime DueDate { get; private set; }

    // Request Summary
    public int TotalAppraisals { get; private set; }
    public string? RequestDescription { get; private set; }
    public string? SpecialRequirements { get; private set; }

    // Status
    public string Status { get; private set; } = null!; // Draft, Sent, Closed, Cancelled
    public int TotalCompaniesInvited { get; private set; }
    public int TotalQuotationsReceived { get; private set; }

    // Selection
    public Guid? SelectedCompanyId { get; private set; }
    public Guid? SelectedQuotationId { get; private set; }
    public DateTime? SelectedAt { get; private set; }
    public string? SelectionReason { get; private set; }

    // Created By
    public Guid RequestedBy { get; private set; }
    public string RequestedByName { get; private set; } = null!;

    private QuotationRequest()
    {
    }

    public static QuotationRequest Create(
        string quotationNumber,
        DateTime dueDate,
        Guid requestedBy,
        string requestedByName,
        string? description = null)
    {
        return new QuotationRequest
        {
            Id = Guid.CreateVersion7(),
            QuotationNumber = quotationNumber,
            RequestDate = DateTime.UtcNow,
            DueDate = dueDate,
            RequestedBy = requestedBy,
            RequestedByName = requestedByName,
            RequestDescription = description,
            Status = "Draft",
            TotalAppraisals = 0,
            TotalCompaniesInvited = 0,
            TotalQuotationsReceived = 0
        };
    }

    public QuotationRequestItem AddItem(
        Guid appraisalId,
        string appraisalNumber,
        string propertyType,
        string? propertyLocation = null,
        decimal? estimatedValue = null)
    {
        var itemNumber = _items.Count + 1;
        var item = QuotationRequestItem.Create(
            Id, appraisalId, itemNumber, appraisalNumber, propertyType, propertyLocation, estimatedValue);
        _items.Add(item);
        TotalAppraisals = _items.Count;
        return item;
    }

    public QuotationInvitation InviteCompany(Guid companyId)
    {
        if (_invitations.Any(i => i.CompanyId == companyId))
            throw new InvalidOperationException("Company already invited");

        var invitation = QuotationInvitation.Create(Id, companyId);
        _invitations.Add(invitation);
        TotalCompaniesInvited = _invitations.Count;
        return invitation;
    }

    public void Send()
    {
        if (Status != "Draft")
            throw new InvalidOperationException($"Cannot send RFQ in status '{Status}'");

        if (!_items.Any())
            throw new InvalidOperationException("Cannot send RFQ without items");

        if (!_invitations.Any())
            throw new InvalidOperationException("Cannot send RFQ without company invitations");

        Status = "Sent";
    }

    public void SelectQuotation(Guid companyId, Guid quotationId, string? reason = null)
    {
        if (Status != "Sent")
            throw new InvalidOperationException($"Cannot select quotation for RFQ in status '{Status}'");

        SelectedCompanyId = companyId;
        SelectedQuotationId = quotationId;
        SelectedAt = DateTime.UtcNow;
        SelectionReason = reason;
        Status = "Closed";

        // Mark the winning quotation
        var winningQuotation = _quotations.FirstOrDefault(q => q.Id == quotationId);
        winningQuotation?.MarkAsWinner();
    }

    public void Cancel()
    {
        if (Status == "Closed")
            throw new InvalidOperationException("Cannot cancel a closed RFQ");

        Status = "Cancelled";
    }

    public void SetSpecialRequirements(string requirements)
    {
        SpecialRequirements = requirements;
    }

    internal void AddQuotation(CompanyQuotation quotation)
    {
        _quotations.Add(quotation);
        TotalQuotationsReceived = _quotations.Count;
    }
}