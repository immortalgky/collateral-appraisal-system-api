namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Payment history for an AppraisalFee.
/// Tracks individual payments for partial/full payment tracking.
/// </summary>
public class AppraisalFeePaymentHistory : Entity<Guid>
{
    public Guid AppraisalFeeId { get; private set; }

    // Payment Details
    public decimal PaymentAmount { get; private set; }
    public DateTime PaymentDate { get; private set; }
    public string? PaymentMethod { get; private set; } // Cash, Transfer, Cheque
    public string? PaymentReference { get; private set; }

    // Remarks
    public string? Remarks { get; private set; }

    private AppraisalFeePaymentHistory()
    {
        // For EF Core
    }

    public static AppraisalFeePaymentHistory Create(
        Guid appraisalFeeId,
        decimal paymentAmount,
        DateTime paymentDate,
        string? paymentMethod = null,
        string? paymentReference = null,
        string? remarks = null)
    {
        return new AppraisalFeePaymentHistory
        {
            //Id = Guid.CreateVersion7(),
            AppraisalFeeId = appraisalFeeId,
            PaymentAmount = paymentAmount,
            PaymentDate = paymentDate,
            PaymentMethod = paymentMethod,
            PaymentReference = paymentReference,
            Remarks = remarks
        };
    }

    public void Update(decimal paymentAmount, DateTime paymentDate)
    {
        PaymentAmount = paymentAmount;
        PaymentDate = paymentDate;
    }
}