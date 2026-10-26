namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Fee summary per assignment. One AppraisalFee per assignment.
/// Aggregates fee items and tracks payment status.
/// </summary>
public class AppraisalFee : Entity<Guid>
{
    /// <summary>The appraisal-fee line (FeeCode "01"): the one base fee the case is billed on.</summary>
    public const string BaseFeeCode = "01";

    /// <summary>The parameter group that maintains the valid fee codes and their names.</summary>
    public const string FeeTypeParameterGroup = "TypeOfFee";

    /// <summary>VAT % a fee starts with; the one definition for the aggregate and for read paths with no fee row yet.</summary>
    public const decimal DefaultVatRate = 7.00m;

    public Guid AssignmentId { get; private set; }

    /// <summary>
    /// A shell that carries nothing from the request (no payment type, notes or selling price): the row an admin's
    /// first fee edit creates when the request had no fee data. Assignment treats it as "no fee row": before
    /// admin fee editing existed such an appraisal got no base fee at all, and the preview agrees. A request's own
    /// row always has a payment type (Fee.Validate), so an IBG row with a null selling price is not blank.
    /// </summary>
    public static bool IsBlankShell(string? feePaymentType, string? feeNotes, decimal? totalSellingPrice) =>
        feePaymentType is null && feeNotes is null && totalSellingPrice is null;

    public bool IsBlank => IsBlankShell(FeePaymentType, FeeNotes, TotalSellingPrice);

    // Fee Metadata (from Request)
    public string? FeePaymentType { get; private set; }
    public string? FeeNotes { get; private set; }
    public decimal? TotalSellingPrice { get; private set; }

    // Fee Totals (calculated from FeeItems)
    public decimal TotalFeeBeforeVAT { get; private set; }
    public decimal VATRate { get; private set; } = DefaultVatRate;
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
            VATRate = DefaultVatRate,
            VATAmount = 0,
            TotalFeeAfterVAT = 0,
            BankAbsorbAmount = 0,
            CustomerPayableAmount = 0,
            TotalPaidAmount = 0,
            OutstandingAmount = 0,
            PaymentStatus = "NotPaid"
        };
    }

    /// <summary>VAT on an ex-VAT amount, rounded the way the decimal(18,2) column stores it. Shared with the fee preview.</summary>
    public static decimal CalculateVat(decimal exVatAmount, decimal vatRate) =>
        Math.Round(exVatAmount * vatRate / 100, 2, MidpointRounding.AwayFromZero);

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
        if (item.IsFinalised)
            throw new InvalidOperationException(
                "Cannot modify a fee that has already been approved or rejected.");
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
            .Where(i => i.IsBillable)
            .Sum(i => i.FeeAmount);
        VATAmount = CalculateVat(TotalFeeBeforeVAT, VATRate);
        TotalFeeAfterVAT = TotalFeeBeforeVAT + VATAmount;
        // Re-clamp the bank absorb so it can never exceed the (possibly reduced) total. Without
        // this, removing/editing a fee item below a prior full absorb would leave a stale
        // BankAbsorbAmount, drive CustomerPayableAmount negative, and make the next fee update
        // PATCH (which re-sends the stale amount) trip the SetBankAbsorb bounds guard.
        if (BankAbsorbAmount > TotalFeeAfterVAT)
            BankAbsorbAmount = TotalFeeAfterVAT;
        CustomerPayableAmount = TotalFeeAfterVAT - BankAbsorbAmount;
        OutstandingAmount = TotalFeeAfterVAT - TotalPaidAmount;
        UpdatePaymentStatus();
    }

    /// <summary>
    /// Re-evaluates the whole set of company-added fee items as a single group based on the
    /// policy verdict, then recalculates totals.
    ///
    /// "Company-added" is defined as Source == "User" AND ApprovalStatus != "Approved".
    /// The base appraisal fee (added by AssignmentFeeService), construction-inspection fees, and
    /// any other system-created items have Source == "System" and are never touched here.
    /// Bank-approved items (ApprovalStatus == "Approved") are excluded — their state is final.
    /// </summary>
    /// <param name="requiresApproval">
    /// true  → set all eligible items to Pending (RequiresApproval=true, ApprovalStatus="Pending")
    /// false → set all eligible items to Billable (RequiresApproval=false, ApprovalStatus=null)
    /// </param>
    public void ReevaluateAddedFees(bool requiresApproval)
    {
        // Only user-entered items in a non-final state (draft/pending). Approved and Rejected
        // fees are final and must not be flipped back.
        var companyAdded = _items
            .Where(i => i.IsActiveAddedFee)
            .ToList();

        foreach (var item in companyAdded)
        {
            if (requiresApproval)
                item.MakePendingApproval();
            else
                item.MakeBillable();
        }

        RecalculateFromItems();
    }

    /// <summary>
    /// Null, empty and whitespace all mean "no payment type" and are stored as null: the screen sends '' for a stored
    /// null, and an empty string would turn a blank shell (<see cref="IsBlank"/>) into a priced one.
    /// </summary>
    public void SetFeePaymentType(string? feePaymentType)
    {
        FeePaymentType = string.IsNullOrWhiteSpace(feePaymentType) ? null : feePaymentType;
    }

    public void SetBankAbsorb(decimal amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(
                nameof(amount), $"BankAbsorbAmount cannot be negative (got {amount:F2}).");

        // The upper bound only applies once a billable total exists. On a freshly-created shell fee
        // the items are added later (AssignmentFeeService.EnsureAssignmentFeeItemsAsync once the real
        // assignee is known), so TotalFeeAfterVAT is still 0 and the absorb amount carried on the
        // request can't be validated against it yet — defer it. RecalculateFromItems re-clamps the
        // amount down to the real total when items materialise.
        if (TotalFeeAfterVAT > 0 && amount > TotalFeeAfterVAT)
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                $"BankAbsorbAmount must be between 0 and TotalFeeAfterVAT ({TotalFeeAfterVAT:F2}) (got {amount:F2}).");

        BankAbsorbAmount = amount;
        // Floor at 0 so the shell never persists a negative customer-payable while it transiently
        // holds absorb > total (0). Corrected on the next RecalculateFromItems once items exist.
        CustomerPayableAmount = Math.Max(0m, TotalFeeAfterVAT - BankAbsorbAmount);
        OutstandingAmount = TotalFeeAfterVAT - TotalPaidAmount;
        UpdatePaymentStatus();
    }

    /// <summary>
    /// Admin stage only: puts back the absorb the request asked for after an item change re-clamped it to the
    /// partial total. Nothing is billed yet, and assignment re-applies it (capped at the final total), so a
    /// half-built fee must not shrink it for good. <see cref="CustomerPayableAmount"/> stays floored at 0 meanwhile.
    /// </summary>
    public void RestoreRequestedBankAbsorb(decimal requested)
    {
        BankAbsorbAmount = requested;
        CustomerPayableAmount = Math.Max(0m, TotalFeeAfterVAT - requested);
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
