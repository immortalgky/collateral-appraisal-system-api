namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Appraisal fee entity.
/// Tracks all fees associated with an appraisal.
/// </summary>
public class AppraisalFee : Entity<Guid>
{
    // Core Properties
    public Guid AppraisalId { get; private set; }
    public Guid? AssignmentId { get; private set; }

    // Fee Type
    public string FeeType { get; private set; } =
        null!; // AppraisalFee, TravelExpense, UrgentSurcharge, RevisionFee, AdditionalSurvey

    public string FeeCategory { get; private set; } = null!; // Internal, External
    public string Description { get; private set; } = null!;

    // Amount
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "THB";

    // Tax (for external fees)
    public decimal? VATRate { get; private set; }
    public decimal? VATAmount { get; private set; }
    public decimal? WithholdingTaxRate { get; private set; }
    public decimal? WithholdingTaxAmount { get; private set; }
    public decimal NetAmount { get; private set; }

    // Billing (for external)
    public bool IsBillableToCustomer { get; private set; }
    public string? InvoiceNumber { get; private set; }
    public DateTime? InvoiceDate { get; private set; }
    public string? PaymentStatus { get; private set; } // Pending, Invoiced, Paid
    public DateTime? PaymentDate { get; private set; }

    // For internal cost tracking
    public string? CostCenter { get; private set; }

    // Fee Items (for 3-table design)
    private readonly List<AppraisalFeeItem> _items = [];
    public IReadOnlyList<AppraisalFeeItem> Items => _items.AsReadOnly();

    private AppraisalFee()
    {
    }

    public static AppraisalFee Create(
        Guid appraisalId,
        string feeType,
        string feeCategory,
        string description,
        decimal amount)
    {
        return new AppraisalFee
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId,
            FeeType = feeType,
            FeeCategory = feeCategory,
            Description = description,
            Amount = amount,
            NetAmount = amount,
            PaymentStatus = "Pending"
        };
    }

    public void SetAssignment(Guid assignmentId)
    {
        AssignmentId = assignmentId;
    }

    public void SetTax(decimal vatRate, decimal withholdingTaxRate)
    {
        VATRate = vatRate;
        VATAmount = Amount * vatRate / 100;
        WithholdingTaxRate = withholdingTaxRate;
        WithholdingTaxAmount = Amount * withholdingTaxRate / 100;
        NetAmount = Amount + VATAmount.Value - WithholdingTaxAmount.Value;
    }

    public void SetBillable(bool isBillable, string? costCenter = null)
    {
        IsBillableToCustomer = isBillable;
        CostCenter = costCenter;
    }

    public void RecordInvoice(string invoiceNumber, DateTime invoiceDate)
    {
        InvoiceNumber = invoiceNumber;
        InvoiceDate = invoiceDate;
        PaymentStatus = "Invoiced";
    }

    public void RecordPayment(DateTime paymentDate)
    {
        PaymentDate = paymentDate;
        PaymentStatus = "Paid";
    }

    public AppraisalFeeItem AddItem(string itemType, string description, int quantity, decimal unitPrice)
    {
        var item = AppraisalFeeItem.Create(Id, itemType, description, quantity, unitPrice);
        _items.Add(item);
        RecalculateTotals();
        return item;
    }

    private void RecalculateTotals()
    {
        Amount = _items.Sum(i => i.Amount);
        NetAmount = _items.Sum(i => i.NetAmount);
    }
}