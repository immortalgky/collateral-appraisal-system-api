namespace Appraisal.Domain.Invoices;

public class Invoice : Entity<Guid>
{
    public string? InvoiceNumber { get; private set; }
    public Guid CompanyId { get; private set; }
    public string Status { get; private set; } = "Draft";
    public decimal TotalAmount { get; private set; }
    public string? Notes { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public string? PaymentReference { get; private set; }
    public string? PaymentMethod { get; private set; }
    public DateOnly? PaymentDate { get; private set; }
    public string? ApprovedBy { get; private set; }
    public DateTime? ApprovedAt { get; private set; }

    private readonly List<InvoiceItem> _items = [];
    public IReadOnlyList<InvoiceItem> Items => _items.AsReadOnly();

    private Invoice() { }

    public static Invoice Create(Guid companyId)
    {
        return new Invoice
        {
            Id = Guid.CreateVersion7(),
            CompanyId = companyId,
            Status = "Draft",
            TotalAmount = 0
        };
    }

    public void AddItem(
        Guid assignmentId,
        Guid appraisalFeeId,
        decimal bankAbsorbAmount,
        string? appraisalNumber,
        string? customerName,
        string? productType,
        decimal feeBeforeVAT,
        decimal vatRate,
        decimal vatAmount,
        decimal totalFeeAfterVAT,
        DateTime? receivedDate)
    {
        var item = new InvoiceItem(
            Guid.CreateVersion7(),
            Id,
            assignmentId,
            appraisalFeeId,
            bankAbsorbAmount,
            appraisalNumber,
            customerName,
            productType,
            feeBeforeVAT,
            vatRate,
            vatAmount,
            totalFeeAfterVAT,
            receivedDate);

        _items.Add(item);
        RecalculateTotalAmount();
    }

    public void ClearItems()
    {
        _items.Clear();
        TotalAmount = 0;
    }

    public void Submit()
    {
        if (Status != "Draft")
            throw new InvalidOperationException("Only Draft invoices can be submitted.");
        if (_items.Count == 0)
            throw new InvalidOperationException("Cannot submit an invoice with no items.");

        Status = "Submitted";
        SubmittedAt = DateTime.UtcNow;
    }

    public void Approve(string approvedBy, string? paymentReference, string? paymentMethod, DateOnly? paymentDate)
    {
        if (Status != "Submitted")
            throw new InvalidOperationException("Only Submitted invoices can be approved.");

        Status = "Approved";
        ApprovedBy = approvedBy;
        ApprovedAt = DateTime.UtcNow;
        PaymentReference = paymentReference;
        PaymentMethod = paymentMethod;
        PaymentDate = paymentDate;
    }

    internal void SetInvoiceNumber(string number)
    {
        InvoiceNumber = number;
    }

    public void SetNotes(string? notes)
    {
        Notes = notes;
    }

    private void RecalculateTotalAmount()
    {
        TotalAmount = _items.Sum(i => i.BankAbsorbAmount);
    }
}
