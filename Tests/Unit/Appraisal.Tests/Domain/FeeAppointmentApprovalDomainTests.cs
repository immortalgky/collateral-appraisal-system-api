using Appraisal.Domain.Appraisals;

namespace Appraisal.Tests.Domain;

/// <summary>
/// Domain tests for the inline-edit fee/appointment approval flow.
///
/// Covers the regressions found in review:
///   B1 — ReevaluateAddedFees must only touch company-added, not-yet-approved items;
///        the base appraisal fee must never be re-pended / dropped from the billable total.
///   H1 — AppraisalFeeItem.Approve() must clear the draft markers (RequiresApproval /
///        ApprovalSubmittedAt) so the FE "awaiting approval" badge clears, while the item
///        stays billable (ApprovalStatus == "Approved").
/// Plus the appointment draft lifecycle (flag → submit → approve/reject → clear) and the
/// cumulative-added predicate used by the Add/Update/Remove fee handlers.
/// </summary>
public class FeeAppointmentApprovalDomainTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Fee with a base "01" appraisal fee item that is NOT company-added.</summary>
    private static AppraisalFee NewFeeWithBase(decimal baseAmount = 5000m)
    {
        var fee = AppraisalFee.Create(Guid.NewGuid());
        fee.AddItem("01", "Appraisal Fee", baseAmount, requiresApproval: false);
        return fee;
    }

    /// <summary>Adds a user-entered fee item the same way AddFeeItemCommandHandler does.</summary>
    private static AppraisalFeeItem AddCompanyFee(AppraisalFee fee, string code, decimal amount)
    {
        var item = fee.AddItem(code, "desc", amount, requiresApproval: false);
        item.MarkAsUserAdded();
        return item;
    }

    // ── B1: ReevaluateAddedFees ──────────────────────────────────────────────

    [Fact]
    public void ReevaluateAddedFees_ApprovalNeeded_PendsOnlyCompanyAddedItems_BaseStaysBillable()
    {
        var fee = NewFeeWithBase(5000m);
        var travel = AddCompanyFee(fee, "02", 200m);

        fee.ReevaluateAddedFees(requiresApproval: true);

        var baseItem = fee.Items.Single(i => i.FeeCode == "01");
        Assert.False(baseItem.RequiresApproval);
        Assert.Null(baseItem.ApprovalStatus);

        Assert.True(travel.RequiresApproval);
        Assert.Equal("Pending", travel.ApprovalStatus);

        // The pending company fee is excluded; the customer's core fee stays on the bill.
        Assert.Equal(5000m, fee.TotalFeeBeforeVAT);
    }

    [Fact]
    public void ReevaluateAddedFees_NoApprovalNeeded_KeepsCompanyAddedBillable()
    {
        var fee = NewFeeWithBase(5000m);
        var travel = AddCompanyFee(fee, "02", 200m);

        fee.ReevaluateAddedFees(requiresApproval: false);

        Assert.False(travel.RequiresApproval);
        Assert.Null(travel.ApprovalStatus);
        Assert.Equal(5200m, fee.TotalFeeBeforeVAT);
    }

    [Fact]
    public void ReevaluateAddedFees_FlipsBothDirections_WhenVerdictReverses()
    {
        var fee = NewFeeWithBase(5000m);
        var travel = AddCompanyFee(fee, "02", 4000m);

        // crossed threshold → pending, dropped from billable
        fee.ReevaluateAddedFees(requiresApproval: true);
        Assert.Equal("Pending", travel.ApprovalStatus);
        Assert.Equal(5000m, fee.TotalFeeBeforeVAT);

        // lowered/removed → back below threshold → billable again
        fee.ReevaluateAddedFees(requiresApproval: false);
        Assert.False(travel.RequiresApproval);
        Assert.Null(travel.ApprovalStatus);
        Assert.Equal(9000m, fee.TotalFeeBeforeVAT);
    }

    [Fact]
    public void ReevaluateAddedFees_NeverRePendsBankApprovedItems()
    {
        var fee = NewFeeWithBase(5000m);
        var approved = AddCompanyFee(fee, "02", 1000m);
        approved.MakePendingApproval();
        approved.Approve("P5229");
        Assert.Equal("Approved", approved.ApprovalStatus);

        // A later re-evaluation must leave the bank-approved item untouched.
        fee.ReevaluateAddedFees(requiresApproval: true);

        Assert.Equal("Approved", approved.ApprovalStatus);
        Assert.False(approved.RequiresApproval);
        Assert.Equal(6000m, fee.TotalFeeBeforeVAT); // base + approved item
    }

    [Fact]
    public void CumulativeAddedTotal_ExcludesBaseFeeAndBankApprovedItems()
    {
        // Mirrors the predicate in Add/Update/RemoveFeeItemCommandHandler.
        var fee = NewFeeWithBase(5000m);
        AddCompanyFee(fee, "02", 200m);                 // counts
        var approved = AddCompanyFee(fee, "03", 300m);  // excluded once approved
        approved.MakePendingApproval();
        approved.Approve("P5229");

        var cumulative = fee.Items
            .Where(i => i.Source == FeeItemSource.User && i.ApprovalStatus != "Approved")
            .Sum(i => i.FeeAmount);

        Assert.Equal(200m, cumulative); // base is System source; "03" approved → excluded
    }

    [Fact]
    public void FeeSource_DefaultsToSystem_AndUserAddedIsUser()
    {
        var fee = NewFeeWithBase(5000m);
        var baseItem = fee.Items.Single(i => i.FeeCode == "01");
        Assert.Equal(FeeItemSource.System, baseItem.Source); // system-generated by default

        var travel = AddCompanyFee(fee, "02", 200m);
        Assert.Equal(FeeItemSource.User, travel.Source); // inline-added → User
    }

    // ── H1: AppraisalFeeItem.Approve clears draft markers ────────────────────

    [Fact]
    public void FeeItemApprove_ClearsDraftMarkers_ButStaysBillable()
    {
        var fee = NewFeeWithBase(5000m);
        var item = AddCompanyFee(fee, "02", 1000m);
        item.MakePendingApproval();
        item.MarkApprovalSubmitted();
        Assert.NotNull(item.ApprovalSubmittedAt);

        item.Approve("P5229");

        Assert.Equal("Approved", item.ApprovalStatus);
        Assert.False(item.RequiresApproval);
        Assert.Null(item.ApprovalSubmittedAt);

        fee.RecalculateFromItems();
        Assert.Equal(6000m, fee.TotalFeeBeforeVAT); // approved item now billable
    }

    [Fact]
    public void FeeItemApprove_Throws_WhenNotPending()
    {
        var fee = NewFeeWithBase();
        var item = AddCompanyFee(fee, "02", 100m); // billable, ApprovalStatus == null
        Assert.Throws<InvalidOperationException>(() => item.Approve("P5229"));
    }

    [Fact]
    public void FeeItemReject_SetsRejected_ClearsSubmitted_StaysNonBillable()
    {
        var fee = NewFeeWithBase(5000m);
        var item = AddCompanyFee(fee, "02", 1000m);
        item.MakePendingApproval();
        item.MarkApprovalSubmitted();

        item.Reject("P5229", "Test");

        Assert.Equal("Rejected", item.ApprovalStatus); // not nulled
        Assert.Null(item.ApprovalSubmittedAt);          // awaiting badge clears
        Assert.True(item.RequiresApproval);             // kept → excluded from billable
        Assert.Equal("Test", item.RejectionReason);

        fee.RecalculateFromItems();
        Assert.Equal(5000m, fee.TotalFeeBeforeVAT); // rejected fee not billed
    }

    [Fact]
    public void ReevaluateAddedFees_DoesNotFlipFinalisedItems()
    {
        var fee = NewFeeWithBase(5000m);
        var rejected = AddCompanyFee(fee, "02", 1000m);
        rejected.MakePendingApproval();
        rejected.Reject("P5229", "no");

        // A later add triggers re-evaluation — the rejected fee must stay Rejected.
        fee.ReevaluateAddedFees(requiresApproval: true);
        Assert.Equal("Rejected", rejected.ApprovalStatus);

        fee.ReevaluateAddedFees(requiresApproval: false);
        Assert.Equal("Rejected", rejected.ApprovalStatus); // never made billable
    }

    [Fact]
    public void UpdateItem_Throws_WhenItemFinalised()
    {
        var fee = NewFeeWithBase(5000m);
        var item = AddCompanyFee(fee, "02", 1000m);
        item.Id = Guid.NewGuid(); // Create() leaves Id empty (EF assigns); set a unique one for lookup
        item.MakePendingApproval();
        item.Approve("P5229");

        Assert.Throws<InvalidOperationException>(
            () => fee.UpdateItem(item.Id, "02", "changed", 2000m));
    }

    [Fact]
    public void FeeItemClearApprovalMarkers_ResetsForFreshDraft()
    {
        var fee = NewFeeWithBase();
        var item = AddCompanyFee(fee, "02", 100m);
        item.MakePendingApproval();
        item.MarkApprovalSubmitted();

        item.ClearApprovalMarkers();

        Assert.False(item.RequiresApproval);
        Assert.Null(item.ApprovalStatus);
        Assert.Null(item.ApprovalSubmittedAt);
    }

    // ── Appointment draft lifecycle ──────────────────────────────────────────

    [Fact]
    public void Reschedule_SetsPending_AndFlagRequiresApproval()
    {
        var appt = Appointment.Create(Guid.NewGuid(), new DateTime(2026, 6, 10, 9, 0, 0), "company");
        appt.Approve("system"); // start effective

        appt.Reschedule("company", new DateTime(2026, 6, 14, 9, 0, 0),"");
        appt.FlagRequiresApproval();

        Assert.Equal("Pending", appt.Status);
        Assert.True(appt.RequiresApproval);
        Assert.Equal(1, appt.RescheduleCount);
    }

    [Fact]
    public void AppointmentApprove_ClearsApprovalFlags_AndSetsApproved()
    {
        var appt = Appointment.Create(Guid.NewGuid(), new DateTime(2026, 6, 14, 9, 0, 0), "company");
        appt.FlagRequiresApproval();
        appt.MarkApprovalSubmitted();
        Assert.True(appt.RequiresApproval);
        Assert.NotNull(appt.ApprovalSubmittedAt);

        appt.Approve("system");

        Assert.Equal("Approved", appt.Status);
        Assert.False(appt.RequiresApproval);
        Assert.Null(appt.ApprovalSubmittedAt);
    }

    [Fact]
    public void RejectReschedule_RevertsToPriorDate_AndDecrementsCount()
    {
        var d1 = new DateTime(2026, 6, 10, 9, 0, 0);
        var d2 = new DateTime(2026, 6, 14, 9, 0, 0);
        var appt = Appointment.Create(Guid.NewGuid(), d1, "company");
        appt.Approve("system");

        appt.Reschedule("company", d2, "");
        Assert.Equal(d2, appt.AppointmentDateTime);
        Assert.Equal(1, appt.RescheduleCount);

        appt.RejectReschedule("system", "weekend not allowed");

        Assert.Equal(d1, appt.AppointmentDateTime);
        Assert.Equal(0, appt.RescheduleCount);
    }

    [Fact]
    public void AppointmentClearApprovalMarkers_ResetsForFreshDraft()
    {
        var appt = Appointment.Create(Guid.NewGuid(), new DateTime(2026, 6, 14, 9, 0, 0), "company");
        appt.FlagRequiresApproval();
        appt.MarkApprovalSubmitted();

        appt.ClearApprovalMarkers();

        Assert.False(appt.RequiresApproval);
        Assert.Null(appt.ApprovalSubmittedAt);
    }
}
