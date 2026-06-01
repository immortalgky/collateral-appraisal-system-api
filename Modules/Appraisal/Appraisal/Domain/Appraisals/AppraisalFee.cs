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

    // Construction Inspection Fee — only set when at least one property is a building under construction.
    public decimal? ConstructionInspectionFeeAmount { get; private set; }

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

    public AppraisalFeeItem AddItem(string feeCode, string feeDescription, decimal feeAmount,
        bool requiresApproval = false)
    {
        var item = AppraisalFeeItem.Create(Id, feeCode, feeDescription, feeAmount, requiresApproval);
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
        // Only count items that are approved or do not require approval.
        // Items with ApprovalStatus == "Pending" or "Rejected" are excluded from billable totals
        // (consumption-point gate per the FeeAppointmentApproval feature design).
        TotalFeeBeforeVAT = _items
            .Where(i => !i.RequiresApproval || i.ApprovalStatus == "Approved")
            .Sum(i => i.FeeAmount);
        VATAmount = TotalFeeBeforeVAT * VATRate / 100;
        TotalFeeAfterVAT = TotalFeeBeforeVAT + VATAmount;
        CustomerPayableAmount = TotalFeeAfterVAT - BankAbsorbAmount;
        OutstandingAmount = TotalFeeAfterVAT - TotalPaidAmount;
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
        OutstandingAmount = TotalFeeAfterVAT - TotalPaidAmount;
        UpdatePaymentStatus();
    }

    public void SetConstructionInspectionFee(decimal? amount)
    {
        ConstructionInspectionFeeAmount = amount;
    }

    public void RecordPayment(decimal paymentAmount, DateTime paymentDate,
        string? paymentMethod = null, string? paymentReference = null, string? remarks = null)
    {
        AddPayment(paymentAmount, paymentDate, paymentMethod, paymentReference, remarks, PaymentSource.Customer);
        RecalculateFromPayments();
    }

    /// <summary>
    /// Settles the bank-absorbed portion of this fee.
    /// Inserts a synthetic BankAbsorb payment-history row (idempotent — will not insert a second row).
    /// Call this when the invoice is marked Paid.
    /// </summary>
    public void SettleBankAbsorbed(DateTime paidDate, string? reference = null, string? remarks = null)
    {
        if (BankAbsorbAmount <= 0)
            return;

        // Idempotency: only insert once
        if (_paymentHistory.Any(p => p.Source == PaymentSource.BankAbsorb))
            return;

        AddPayment(BankAbsorbAmount, paidDate, paymentMethod: null, reference, remarks, PaymentSource.BankAbsorb);
        RecalculateFromPayments();
    }

    private void AddPayment(
        decimal paymentAmount,
        DateTime paymentDate,
        string? paymentMethod,
        string? paymentReference,
        string? remarks,
        string source)
    {
        var payment = AppraisalFeePaymentHistory.Create(
            Id, paymentAmount, paymentDate, paymentMethod, paymentReference, remarks, source);
        _paymentHistory.Add(payment);
    }

    public void UpdatePayment(Guid paymentId, decimal paymentAmount, DateTime paymentDate)
    {
        var payment = _paymentHistory.FirstOrDefault(p => p.Id == paymentId);
        if (payment == null)
            throw new NotFoundException($"Payment for {paymentDate} not found");

        if (payment.Source == PaymentSource.BankAbsorb)
            throw new InvalidOperationException("Bank-absorbed settlement payments cannot be edited.");

        payment.Update(paymentAmount, paymentDate);

        RecalculateFromPayments();
    }

    public void RemovePayment(Guid paymentId)
    {
        var payment = _paymentHistory.FirstOrDefault(p => p.Id == paymentId);
        if (payment == null)
            throw new NotFoundException($"Payment {paymentId} not found");

        if (payment.Source == PaymentSource.BankAbsorb)
            throw new InvalidOperationException("Bank-absorbed settlement payments cannot be deleted.");

        _paymentHistory.Remove(payment);

        RecalculateFromPayments();
    }

    public void RecalculateFromPayments()
    {
        TotalPaidAmount = _paymentHistory.Sum(p => p.PaymentAmount);
        OutstandingAmount = TotalFeeAfterVAT - TotalPaidAmount;
        UpdatePaymentStatus();
    }

    private void UpdatePaymentStatus()
    {
        if (TotalFeeAfterVAT <= 0)
        {
            PaymentStatus = "NotPaid";
            return;
        }

        if (TotalPaidAmount >= TotalFeeAfterVAT)
        {
            PaymentStatus = "Paid";
            return;
        }

        // Customer has paid their share but the bank absorb row has not been inserted yet
        var customerPaid = _paymentHistory
            .Where(p => p.Source != PaymentSource.BankAbsorb)
            .Sum(p => p.PaymentAmount);
        var bankSettled = BankAbsorbAmount <= 0
                          || _paymentHistory.Any(p => p.Source == PaymentSource.BankAbsorb);

        if (customerPaid >= CustomerPayableAmount && BankAbsorbAmount > 0 && !bankSettled)
        {
            PaymentStatus = "PendingInvoice";
            return;
        }

        PaymentStatus = TotalPaidAmount > 0 ? "Partial" : "NotPaid";
    }
}