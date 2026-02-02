namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Payment history for fee items.
/// Tracks individual payments, supports partial payments and refunds.
/// </summary>
public class AppraisalFeePaymentHistory : Entity<Guid>
{
    public Guid AppraisalFeeItemId { get; private set; }

    // Payment Details
    public decimal PaidAmount { get; private set; }
    public DateTime PaymentDate { get; private set; }
    public string PaymentMethod { get; private set; } = null!;  // Transfer, Cash, Check, CreditCard
    public string? PaymentReference { get; private set; }

    // Status
    public string Status { get; private set; } = null!;  // Pending, Paid, Refunded, Cancelled

    // Refund (if applicable)
    public decimal? RefundAmount { get; private set; }
    public DateTime? RefundDate { get; private set; }
    public string? RefundReason { get; private set; }

    private AppraisalFeePaymentHistory() { }

    public static AppraisalFeePaymentHistory Create(
        Guid appraisalFeeItemId,
        decimal paidAmount,
        DateTime paymentDate,
        string paymentMethod,
        string? paymentReference = null)
    {
        return new AppraisalFeePaymentHistory
        {
            Id = Guid.CreateVersion7(),
            AppraisalFeeItemId = appraisalFeeItemId,
            PaidAmount = paidAmount,
            PaymentDate = paymentDate,
            PaymentMethod = paymentMethod,
            PaymentReference = paymentReference,
            Status = "Paid"
        };
    }

    public void Refund(decimal amount, string reason)
    {
        if (amount > PaidAmount)
            throw new InvalidOperationException("Refund amount cannot exceed paid amount");

        RefundAmount = amount;
        RefundDate = DateTime.UtcNow;
        RefundReason = reason;
        Status = "Refunded";
    }

    public void Cancel()
    {
        Status = "Cancelled";
    }
}
