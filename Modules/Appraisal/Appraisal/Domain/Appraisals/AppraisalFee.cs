namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Fee summary per assignment. One AppraisalFee per assignment.
/// Aggregates fee items and tracks payment status.
/// </summary>
public class AppraisalFee : Entity<Guid>
{
    public Guid AssignmentId { get; private set; }

    // Fee Metadata (from Request)
    public string? FeePaymentType { get; private set; }
    public string? FeeNotes { get; private set; }
    public decimal? TotalSellingPrice { get; private set; }

    // Fee Totals (calculated from FeeItems)
    public decimal TotalFeeBeforeVAT { get; private set; }
    public decimal VATRate { get; private set; } = 7.00m;
    public decimal VATAmount { get; private set; }
    public decimal TotalFeeAfterVAT { get; private set; }

    // Bank Absorb
    public decimal BankAbsorbAmount { get; private set; }
    public decimal CustomerPayableAmount { get; private set; }

    // Payment Status
    public decimal TotalPaidAmount { get; private set; }
    public decimal OutstandingAmount { get; private set; }
    public string PaymentStatus { get; private set; } = "Pending"; // Pending, PartialPaid, FullyPaid

    // InspectionFee
    public decimal? InspectionFeeAmount { get; private set; }

    // Fee Items
    private readonly List<AppraisalFeeItem> _items = [];
    public IReadOnlyList<AppraisalFeeItem> Items => _items.AsReadOnly();
    public bool HasItems => _items.Count > 0;

    // Payment History (at fee level, not item level)
    private readonly List<AppraisalFeePaymentHistory> _paymentHistory = [];
    public IReadOnlyList<AppraisalFeePaymentHistory> PaymentHistory => _paymentHistory.AsReadOnly();

    private AppraisalFee()
    {
        // For EF Core
    }

    public static AppraisalFee Create(
        Guid assignmentId,
        string? feePaymentType = null,
        string? feeNotes = null,
        decimal? totalSellingPrice = null)
    {
        return new AppraisalFee
        {
            Id = Guid.CreateVersion7(),
            AssignmentId = assignmentId,
            FeePaymentType = feePaymentType,
            FeeNotes = feeNotes,
            TotalSellingPrice = totalSellingPrice,
            TotalFeeBeforeVAT = 0,
            VATRate = 7.00m,
            VATAmount = 0,
            TotalFeeAfterVAT = 0,
            BankAbsorbAmount = 0,
            CustomerPayableAmount = 0,
            TotalPaidAmount = 0,
            OutstandingAmount = 0,
            PaymentStatus = "NotPaid"
        };
    }

    public AppraisalFeeItem AddItem(string feeCode, string feeDescription, decimal feeAmount)
    {
        var item = AppraisalFeeItem.Create(Id, feeCode, feeDescription, feeAmount);
        _items.Add(item);
        RecalculateFromItems();
        return item;
    }

    public AppraisalFeeItem UpdateItem(Guid feeItemId, string feeCode, string feeDescription, decimal feeAmount)
    {
        var item = _items.FirstOrDefault(i => i.Id == feeItemId);
        if (item == null)
            throw new NotFoundException($"Fee item {feeItemId} not found");
        item.Update(feeCode, feeDescription, feeAmount);
        RecalculateFromItems();
        return item;
    }

    public AppraisalFeeItem RemoveItem(Guid feeItemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == feeItemId);
        if (item == null)
            throw new NotFoundException($"Fee item {feeItemId} not found");

        _items.Remove(item);
        RecalculateFromItems();

        return item;
    }

    public void RecalculateFromItems()
    {
        TotalFeeBeforeVAT = _items.Sum(i => i.FeeAmount);
        VATAmount = TotalFeeBeforeVAT * VATRate / 100;
        TotalFeeAfterVAT = TotalFeeBeforeVAT + VATAmount;
        CustomerPayableAmount = TotalFeeAfterVAT - BankAbsorbAmount;
        OutstandingAmount = CustomerPayableAmount - TotalPaidAmount;
        UpdatePaymentStatus();
    }

    public void SetFeePaymentType(string feePaymentType)
    {
        FeePaymentType = feePaymentType;
    }

    public void SetBankAbsorb(decimal amount)
    {
        BankAbsorbAmount = amount;
        CustomerPayableAmount = TotalFeeAfterVAT - BankAbsorbAmount;
        OutstandingAmount = CustomerPayableAmount - TotalPaidAmount;
        UpdatePaymentStatus();
    }

    public void SetInspectionFee(decimal? amount)
    {
        InspectionFeeAmount = amount;
    }

    public void RecordPayment(decimal paymentAmount, DateTime paymentDate,
        string? paymentMethod = null, string? paymentReference = null, string? remarks = null)
    {
        var payment = AppraisalFeePaymentHistory.Create(
            Id, paymentAmount, paymentDate, paymentMethod, paymentReference, remarks);
        _paymentHistory.Add(payment);

        RecalculateFromPayments();
    }

    public void UpdatePayment(Guid paymentId, decimal paymentAmount, DateTime paymentDate)
    {
        var payment = _paymentHistory.FirstOrDefault(p => p.Id == paymentId);
        if (payment == null)
            throw new NotFoundException($"Payment for {paymentDate} not found");

        payment.Update(paymentAmount, paymentDate);

        RecalculateFromPayments();
    }

    public void RemovePayment(Guid paymentId)
    {
        var payment = _paymentHistory.FirstOrDefault(p => p.Id == paymentId);
        if (payment == null)
            throw new NotFoundException($"Payment {paymentId} not found");

        _paymentHistory.Remove(payment);

        RecalculateFromPayments();
    }

    public void RecalculateFromPayments()
    {
        TotalPaidAmount = _paymentHistory.Sum(p => p.PaymentAmount);
        OutstandingAmount = CustomerPayableAmount - TotalPaidAmount;
        UpdatePaymentStatus();
    }

    private void UpdatePaymentStatus()
    {
        if (TotalPaidAmount >= CustomerPayableAmount && CustomerPayableAmount > 0)
            PaymentStatus = "Paid";
        else if (TotalPaidAmount > 0)
            PaymentStatus = "Partial";
        else
            PaymentStatus = "NotPaid";
    }
}