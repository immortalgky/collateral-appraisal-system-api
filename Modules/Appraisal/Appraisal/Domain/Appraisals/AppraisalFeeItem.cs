namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Individual fee line item within an AppraisalFee.
/// Supports detailed breakdown of fees.
/// </summary>
public class AppraisalFeeItem : Entity<Guid>
{
    public Guid AppraisalFeeId { get; private set; }

    // Item Details
    public string ItemType { get; private set; } = null!;  // AppraisalFee, Travel, Urgent, Survey, Revision
    public string Description { get; private set; } = null!;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }

    // Amounts
    public decimal Amount { get; private set; }
    public decimal? VATRate { get; private set; }
    public decimal? VATAmount { get; private set; }
    public decimal NetAmount { get; private set; }

    // Status
    public string PaymentStatus { get; private set; } = null!;  // Pending, PartiallyPaid, Paid

    // Collection of payment history
    private readonly List<AppraisalFeePaymentHistory> _paymentHistory = [];
    public IReadOnlyList<AppraisalFeePaymentHistory> PaymentHistory => _paymentHistory.AsReadOnly();

    private AppraisalFeeItem() { }

    public static AppraisalFeeItem Create(
        Guid appraisalFeeId,
        string itemType,
        string description,
        int quantity,
        decimal unitPrice)
    {
        var amount = quantity * unitPrice;

        return new AppraisalFeeItem
        {
            Id = Guid.NewGuid(),
            AppraisalFeeId = appraisalFeeId,
            ItemType = itemType,
            Description = description,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Amount = amount,
            NetAmount = amount,
            PaymentStatus = "Pending"
        };
    }

    public void SetVAT(decimal vatRate)
    {
        VATRate = vatRate;
        VATAmount = Amount * vatRate / 100;
        NetAmount = Amount + VATAmount.Value;
    }

    public AppraisalFeePaymentHistory RecordPayment(
        decimal paidAmount,
        DateTime paymentDate,
        string paymentMethod,
        string? paymentReference = null)
    {
        var payment = AppraisalFeePaymentHistory.Create(
            Id,
            paidAmount,
            paymentDate,
            paymentMethod,
            paymentReference);

        _paymentHistory.Add(payment);

        // Update payment status based on total paid
        var totalPaid = _paymentHistory.Where(p => p.Status == "Paid").Sum(p => p.PaidAmount);
        if (totalPaid >= NetAmount)
            PaymentStatus = "Paid";
        else if (totalPaid > 0)
            PaymentStatus = "PartiallyPaid";

        return payment;
    }
}
