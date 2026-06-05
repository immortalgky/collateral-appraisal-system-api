namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Discriminator constants for AppraisalFeePaymentHistory.Source.
/// "Customer" — a real cash/transfer payment from the customer.
/// "BankAbsorb" — a synthetic settlement row created when the bank pays the invoice.
/// </summary>
public static class PaymentSource
{
    public const string Customer = "Customer";
    public const string BankAbsorb = "BankAbsorb";
}

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

    /// <summary>
    /// Discriminator: "Customer" for real cash/transfer payments; "BankAbsorb" for
    /// the synthetic row inserted when the bank settles the invoice.
    /// </summary>
    public string Source { get; private set; } = PaymentSource.Customer;

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
        string? remarks = null,
        string source = PaymentSource.Customer)
    {
        return new AppraisalFeePaymentHistory
        {
            //Id = Guid.CreateVersion7(),
            AppraisalFeeId = appraisalFeeId,
            PaymentAmount = paymentAmount,
            PaymentDate = paymentDate,
            PaymentMethod = paymentMethod,
            PaymentReference = paymentReference,
            Remarks = remarks,
            Source = source
        };
    }

    public void Update(decimal paymentAmount, DateTime paymentDate)
    {
        PaymentAmount = paymentAmount;
        PaymentDate = paymentDate;
    }
}