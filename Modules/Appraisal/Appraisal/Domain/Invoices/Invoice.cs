namespace Appraisal.Domain.Invoices;

public class Invoice : Entity<Guid>
{
    public string? InvoiceNumber { get; private set; }
    public Guid CompanyId { get; private set; }
    public string Status { get; private set; } = "Pending";
    public decimal TotalAmount { get; private set; }
    public string? Notes { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public string? PaymentOrderNo { get; private set; }
    public DateOnly? PaidDate { get; private set; }
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
            Status = "Pending",
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
        DateTime? submittedDate)
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
            submittedDate);

        _items.Add(item);
        RecalculateTotalAmount();
    }

    public void ClearItems()
    {
        _items.Clear();
        TotalAmount = 0;
    }

    public void SetInvoiceNumber(string? number)
    {
        // Drafts may have a null/blank invoice number; submission enforces non-empty.
        var trimmed = string.IsNullOrWhiteSpace(number) ? null : number.Trim();
        if (trimmed is { Length: > 20 })
            throw new ArgumentException("Invoice number cannot exceed 20 characters.");

        InvoiceNumber = trimmed;
    }

    public void Submit()
    {
        if (Status != "Pending")
            throw new InvalidOperationException("Only Pending invoices can be submitted.");
        if (string.IsNullOrWhiteSpace(InvoiceNumber))
            throw new InvalidOperationException("Invoice number is required before submitting.");
        if (_items.Count == 0)
            throw new InvalidOperationException("Cannot submit an invoice with no items.");

        Status = "Sent";
        SubmittedAt = DateTime.UtcNow;
    }

    public void MarkAsPaid(string approvedBy, string paymentOrderNo, DateOnly paidDate)
    {
        if (Status != "Sent")
            throw new InvalidOperationException("Only Sent invoices can be marked as paid.");
        if (string.IsNullOrWhiteSpace(paymentOrderNo))
            throw new ArgumentException("Payment order number cannot be empty.");
        if (paymentOrderNo.Length > 10)
            throw new ArgumentException("Payment order number cannot exceed 10 characters.");

        Status = "Paid";
        ApprovedBy = approvedBy;
        ApprovedAt = DateTime.UtcNow;
        PaymentOrderNo = paymentOrderNo;
        PaidDate = paidDate;
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
