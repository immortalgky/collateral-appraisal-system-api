namespace Appraisal.Domain.Invoices;

public class InvoiceItem : Entity<Guid>
{
    public Guid InvoiceId { get; private set; }
    public Guid AssignmentId { get; private set; }
    public Guid AppraisalFeeId { get; private set; }
    public decimal BankAbsorbAmount { get; private set; }
    public string? AppraisalNumber { get; private set; }
    public string? CustomerName { get; private set; }
    public string? ProductType { get; private set; }
    public decimal FeeBeforeVAT { get; private set; }
    public decimal VATRate { get; private set; }
    public decimal VATAmount { get; private set; }
    public decimal TotalFeeAfterVAT { get; private set; }
    public DateTime? ReceivedDate { get; private set; }

    private InvoiceItem() { }

    internal InvoiceItem(
        Guid id,
        Guid invoiceId,
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
        Id = id;
        InvoiceId = invoiceId;
        AssignmentId = assignmentId;
        AppraisalFeeId = appraisalFeeId;
        BankAbsorbAmount = bankAbsorbAmount;
        AppraisalNumber = appraisalNumber;
        CustomerName = customerName;
        ProductType = productType;
        FeeBeforeVAT = feeBeforeVAT;
        VATRate = vatRate;
        VATAmount = vatAmount;
        TotalFeeAfterVAT = totalFeeAfterVAT;
        ReceivedDate = receivedDate;
    }
}
