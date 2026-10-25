using System.Text.Json;
using System.Text.Json.Serialization;
using Appraisal.Application.Features.Fees.GetFeePreview;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Appraisal.Infrastructure;
using Auth.Contracts.Companies;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shared.Exceptions;
using static Appraisal.Tests.Application.Services.FeeTestSeed;

namespace Appraisal.Tests.Application.Features;

/// <summary>
/// The read-only fee preview: it must show what <see cref="AssignmentFeeService"/> would materialise (same
/// resolution), for the type/method/company asked about or, when omitted, the appraisal's current assignment.
/// </summary>
public class GetFeePreviewQueryHandlerTests
{
    private readonly AppraisalDbContext _db = NewDb();
    private readonly ICompanyLookupService _companies = Substitute.For<ICompanyLookupService>();

    private GetFeePreviewQueryHandler Handler() => new(_db, BuildService(_db), _companies);

    private Task<FeePreviewResult> RunAsync(
        Guid appraisalId, string? type = null, string? method = null, Guid? companyId = null) =>
        Handler().Handle(new GetFeePreviewQuery(appraisalId, type, method, companyId), default);

    private async Task<(Guid AppraisalId, AppraisalFee Fee)> SeedAsync(
        decimal sellingPrice = 1_000_000m, string type = AppraisalTypes.New, bool assigned = false,
        Guid? prevId = null, string? companyId = null, string assignmentType = "Internal")
    {
        SeedGenericLadder(_db);
        var appraisal = await AddAppraisalAsync(_db, type, prevId);
        var assignment = await AddAssignmentAsync(_db, appraisal.Id, assigned, companyId, assignmentType);
        return (appraisal.Id, await AddFeeAsync(_db, assignment.Id, sellingPrice));
    }

    // ── Tier ──

    [Fact]
    public async Task Tier_ReportsTheMatchedBand_AndWouldBeItem_AndVat()
    {
        var (appraisalId, fee) = await SeedAsync(sellingPrice: 8_000_000m);

        var result = await RunAsync(appraisalId);

        Assert.Equal(fee.Id, result.FeeId);
        Assert.Equal(3_000m, result.SystemBaseAmount);
        Assert.Equal(FeeBaseKinds.Tier, result.BaseSource.Kind);
        Assert.Equal(8_000_000m, result.BaseSource.SellingPrice);
        Assert.Equal(7_000_001m, result.BaseSource.TierMin);
        Assert.Equal(10_000_000m, result.BaseSource.TierMax);
        Assert.Null(result.AdminOverrideAmount);
        var item = Assert.Single(result.Items);
        Assert.Null(item.Id);
        Assert.Equal("01", item.FeeCode);
        Assert.Equal("System", item.Source);
        Assert.Equal((3_000m, 7m, 210m, 3_210m), (result.Subtotal, result.VatRate, result.Vat, result.Total));
    }

    [Fact]
    public async Task Preview_WritesNothing()
    {
        var (appraisalId, _) = await SeedAsync();
        _db.ChangeTracker.Clear();

        await RunAsync(appraisalId);

        Assert.False(_db.ChangeTracker.HasChanges());
        Assert.Equal(0, await _db.AppraisalFeeItems.CountAsync());
    }

    // ── Admin override ──

    [Fact]
    public async Task AdminBase_IsTheOverride_AndReplacesTheWouldBeSystemItem()
    {
        var (appraisalId, fee) = await SeedAsync(sellingPrice: 1_000_000m);
        fee.AddItem("01", "admin base", 3_500m).MarkAsAdminSet();
        fee.AddItem("02", "admin travel", 500m).MarkAsAdminSet();
        await _db.SaveChangesAsync();

        var result = await RunAsync(appraisalId);

        Assert.Equal(2_500m, result.SystemBaseAmount); // what the system would have charged
        Assert.Equal(3_500m, result.AdminOverrideAmount);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, i => Assert.NotNull(i.Id));
        Assert.Equal(["Admin", "Admin"], result.Items.Select(i => i.Source));
        Assert.Equal(4_000m, result.Subtotal);
        Assert.Equal(280m, result.Vat);
        Assert.Equal(4_280m, result.Total);
    }

    [Fact]
    public async Task ItemsAwaitingApproval_AreListedButNotBilled()
    {
        var (appraisalId, fee) = await SeedAsync();
        var pending = fee.AddItem("02", "travel", 400m, requiresApproval: true);
        pending.MarkAsUserAdded();
        await _db.SaveChangesAsync();

        var result = await RunAsync(appraisalId);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2_500m, result.Subtotal);
    }

    // ── No fee row ──

    [Fact]
    public async Task NoFeeRow_ResolvesNothing_BecauseAssignmentWouldSkipIt()
    {
        var appraisal = await AddAppraisalAsync(_db, AppraisalTypes.New);
        await AddAssignmentAsync(_db, appraisal.Id);

        var result = await RunAsync(appraisal.Id);

        Assert.Null(result.FeeId);
        Assert.Null(result.SystemBaseAmount);
        Assert.Equal(FeeBaseKinds.None, result.BaseSource.Kind);
        Assert.Empty(result.Items);
        Assert.Equal(0m, result.Total);
        Assert.Equal(AppraisalFee.DefaultVatRate, result.VatRate); // the fee's own default, not a literal here
    }

    [Fact]
    public async Task UnknownAppraisal_Throws()
    {
        await Assert.ThrowsAsync<AppraisalNotFoundException>(() => RunAsync(Guid.NewGuid()));
    }

    // ── Construction inspection chain ──

    [Fact]
    public async Task Progressive_ShowsTheKeyedInspectionFee_WithThePriorsNumber()
    {
        var prior = await AddAppraisalAsync(_db, AppraisalTypes.New, number: "AP-PRIOR");
        var priorAssignment = await AddAssignmentAsync(_db, prior.Id, assigned: true);
        await AddFeeAsync(_db, priorAssignment.Id, inspectionFee: 3_000m);
        var (appraisalId, _) = await SeedAsync(type: AppraisalTypes.Progressive, prevId: prior.Id);

        var result = await RunAsync(appraisalId);

        Assert.Equal(3_000m, result.SystemBaseAmount);
        Assert.Equal(FeeBaseKinds.ConstructionInspectionFee, result.BaseSource.Kind);
        Assert.Equal("AP-PRIOR", result.BaseSource.AppraisalNumber);
        Assert.Null(result.BaseSource.TierMin);
    }

    [Fact]
    public async Task Progressive_WithoutAKeyedFee_ShowsNoAmount_ButNamesThePrior()
    {
        var prior = await AddAppraisalAsync(_db, AppraisalTypes.Progressive, number: "AP-PRIOR");
        var priorAssignment = await AddAssignmentAsync(_db, prior.Id, assigned: true);
        (await AddFeeAsync(_db, priorAssignment.Id)).AddItem("01", "own", 2_500m);
        await _db.SaveChangesAsync();
        var (appraisalId, _) = await SeedAsync(type: AppraisalTypes.Progressive, prevId: prior.Id);

        var result = await RunAsync(appraisalId);

        Assert.Equal(FeeBaseKinds.None, result.BaseSource.Kind);
        Assert.Null(result.SystemBaseAmount);
        Assert.Equal("AP-PRIOR", result.BaseSource.AppraisalNumber);
    }

    // ── Quotation ──

    [Fact]
    public async Task Quotation_ShowsTheWinnersPrice_EvenForProgressive()
    {
        var winner = Guid.NewGuid();
        var prior = await AddAppraisalAsync(_db, AppraisalTypes.New);
        var priorAssignment = await AddAssignmentAsync(_db, prior.Id, assigned: true);
        await AddFeeAsync(_db, priorAssignment.Id, inspectionFee: 3_000m);
        var (appraisalId, _) = await SeedAsync(type: AppraisalTypes.Progressive, prevId: prior.Id);
        await AddFinalizedQuotationAsync(_db, appraisalId, winner, feeAmount: 4_500m, discount: 300m);
        _companies.GetByIdAsync(winner, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(winner, "Winner Co", null, true));

        var result = await RunAsync(appraisalId, type: "external", method: "quotation", companyId: winner);

        Assert.Equal(4_200m, result.SystemBaseAmount); // fee after discount, ex-VAT
        Assert.Equal(FeeBaseKinds.Quotation, result.BaseSource.Kind);
        Assert.Equal("RFQ-001", result.BaseSource.QuotationNumber);
        Assert.Equal("Winner Co", result.BaseSource.WinnerCompanyName);
    }

    [Fact]
    public async Task Quotation_WithoutAFinalizedWinner_IsPending()
    {
        var (appraisalId, _) = await SeedAsync();

        var result = await RunAsync(appraisalId, type: "external", method: "quotation");

        Assert.Equal(FeeBaseKinds.QuotationPending, result.BaseSource.Kind);
        Assert.Null(result.SystemBaseAmount);
        Assert.Empty(result.Items);
        await _companies.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default); // no winner, nothing to name
    }

    [Fact]
    public async Task Quotation_LegacyItemWithoutBreakdown_ShowsTheQuotedPrice()
    {
        var winner = Guid.NewGuid();
        var (appraisalId, _) = await SeedAsync();
        await AddFinalizedQuotationAsync(_db, appraisalId, winner, feeAmount: 0m, quotedPrice: 800m);

        var result = await RunAsync(appraisalId, type: "external", method: "quotation", companyId: winner);

        Assert.Equal(FeeBaseKinds.Quotation, result.BaseSource.Kind);
        Assert.Equal(800m, result.SystemBaseAmount);
    }

    [Fact]
    public async Task Quotation_WinnerThatNeverPricedThisAppraisal_IsPending()
    {
        var winner = Guid.NewGuid();
        var (appraisalId, _) = await SeedAsync();
        // The RFQ covers this appraisal, but the winner's only item prices another one.
        var rfq = await AddFinalizedQuotationAsync(_db, appraisalId, winner, 4_500m);
        var item = rfq.Quotations.Single().Items.Single();
        typeof(Appraisal.Domain.Quotations.CompanyQuotationItem).GetProperty("AppraisalId")!.SetValue(item, Guid.NewGuid());
        await _db.SaveChangesAsync();

        var result = await RunAsync(appraisalId, type: "external", method: "quotation", companyId: winner);

        Assert.Equal(FeeBaseKinds.QuotationPending, result.BaseSource.Kind);
        Assert.Null(result.SystemBaseAmount);
    }

    [Fact]
    public async Task Quotation_ForACompanyThatDidNotWin_IsPending_NamingTheWinner()
    {
        var winner = Guid.NewGuid();
        var (appraisalId, _) = await SeedAsync();
        await AddFinalizedQuotationAsync(_db, appraisalId, winner, 4_500m);
        _companies.GetByIdAsync(winner, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(winner, "Winner Co", null, true));

        var result = await RunAsync(appraisalId, type: "external", method: "quotation", companyId: Guid.NewGuid());

        Assert.Equal(FeeBaseKinds.QuotationPending, result.BaseSource.Kind);
        Assert.Equal("Winner Co", result.BaseSource.WinnerCompanyName);
        Assert.Null(result.SystemBaseAmount);
    }

    [Theory]
    [InlineData("internal", "quotation")]
    [InlineData("external-offline", "quotation")]
    [InlineData("external", "manual")]
    [InlineData("external", "roundrobin")]
    public async Task OnlyAnInSystemExternalQuotation_IsPricedByQuotation(string type, string method)
    {
        var (appraisalId, _) = await SeedAsync(sellingPrice: 1_000_000m);

        var result = await RunAsync(appraisalId, type, method);

        Assert.Equal(FeeBaseKinds.Tier, result.BaseSource.Kind);
        Assert.Equal(2_500m, result.SystemBaseAmount);
        await _companies.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }

    // ── Omitted parameters follow the actual assignment ──

    [Fact]
    public async Task OmittedParameters_UseTheCurrentQuotationAssignment()
    {
        var winner = Guid.NewGuid();
        var (appraisalId, _) = await SeedAsync(assigned: true, companyId: winner.ToString(), assignmentType: "External");
        // The assignment row's method defaults to "Manual"; make it the quotation the company was assigned by.
        var assignment = await _db.AppraisalAssignments.SingleAsync();
        assignment.Assign("External", assigneeCompanyId: winner.ToString(), assignmentMethod: "Quotation", assignedBy: "test");
        await _db.SaveChangesAsync();
        await AddFinalizedQuotationAsync(_db, appraisalId, winner, 4_500m);

        var result = await RunAsync(appraisalId);

        Assert.Equal(FeeBaseKinds.Quotation, result.BaseSource.Kind);
        Assert.Equal(4_500m, result.SystemBaseAmount);
    }

    [Theory]
    [InlineData("bogus", null)]
    [InlineData(null, "bogus")]
    public async Task UnknownTypeOrMethod_IsABadRequest(string? type, string? method)
    {
        var (appraisalId, _) = await SeedAsync();

        await Assert.ThrowsAsync<BadRequestException>(() => RunAsync(appraisalId, type, method));
    }

    [Fact]
    public async Task Serialized_KeepsEveryNullableKey()
    {
        var (appraisalId, _) = await SeedAsync();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(await RunAsync(appraisalId), options);

        Assert.Contains("\"adminOverrideAmount\":null", json);
        Assert.Contains("\"feeId\":\"", json);
        Assert.Contains("\"appraisalNumber\":null", json);
        Assert.Contains("\"quotationNumber\":null", json);
        Assert.Contains("\"winnerCompanyName\":null", json);
        Assert.Contains("\"id\":null", json);
    }

    // ── Current assignment election ──

    [Fact]
    public async Task NewestRowWins_APendingRowOutranksAnOlderAssignedOne()
    {
        // The Pending row has no AssignedAt; ordering by it alone would sort it behind the older assigned row.
        SeedGenericLadder(_db);
        var appraisal = await AddAppraisalAsync(_db, AppraisalTypes.New);
        var older = await AddAssignmentAsync(_db, appraisal.Id, assigned: true, createdAt: new DateTime(2026, 1, 1));
        await AddFeeAsync(_db, older.Id, sellingPrice: 1_000_000m);
        var newer = await AddAssignmentAsync(_db, appraisal.Id, assigned: false, createdAt: new DateTime(2026, 2, 1));
        var newerFee = await AddFeeAsync(_db, newer.Id, sellingPrice: 8_000_000m);
        newerFee.AddItem("02", "admin travel", 500m).MarkAsAdminSet();
        await _db.SaveChangesAsync();

        var result = await RunAsync(appraisal.Id);

        Assert.Equal(newerFee.Id, result.FeeId);
        Assert.Equal(3_000m, result.SystemBaseAmount); // the newer row's selling price band, not the older one's
        Assert.Contains(result.Items, i => i.FeeCode == "02");
    }

    [Fact]
    public async Task NewestRowWithoutAFee_CopiesTheContextOfTheNewestFee_AndHasNoFeeId()
    {
        SeedGenericLadder(_db);
        var appraisal = await AddAppraisalAsync(_db, AppraisalTypes.New);
        var older = await AddAssignmentAsync(_db, appraisal.Id, assigned: true, createdAt: new DateTime(2026, 1, 1));
        var oldFee = await AddFeeAsync(_db, older.Id, sellingPrice: 8_000_000m);
        oldFee.AddItem("02", "old travel", 500m).MarkAsAdminSet();
        await AddAssignmentAsync(_db, appraisal.Id, assigned: false, createdAt: new DateTime(2026, 2, 1));
        await _db.SaveChangesAsync();

        var result = await RunAsync(appraisal.Id);

        Assert.Null(result.FeeId);                    // the current row has no fee yet
        Assert.Equal(3_000m, result.SystemBaseAmount); // context (selling price) copied from the older fee
        Assert.DoesNotContain(result.Items, i => i.FeeCode == "02"); // its items are not carried over
    }

    // ── A system base left by an earlier source (route-back) ──

    [Fact]
    public async Task PendingRowWithAStaleSystemBase_ShowsWhatAssignmentWouldBill_NotTheOldLine()
    {
        // Assignment re-prices the system base on a genuine (re)assignment, so the preview must show that.
        var (appraisalId, fee) = await SeedAsync(sellingPrice: 8_000_000m);
        var stale = fee.AddItem("01", "old quotation base", 9_000m);
        fee.AddItem("02", "admin travel", 500m).MarkAsAdminSet();
        await _db.SaveChangesAsync();

        var result = await RunAsync(appraisalId);

        var baseLine = Assert.Single(result.Items, i => i.FeeCode == "01");
        // The would-be line carries the STORED item's id, so "Edit amount" updates it (the server turns it into the
        // admin's base) rather than posting a second 01.
        Assert.Equal(stale.Id, baseLine.Id);
        Assert.Equal(3_000m, baseLine.Amount);          // the tier band for 8,000,000
        Assert.Equal(3_500m, result.Subtotal);          // 3,000 + 500, the old 9,000 gone
        Assert.Equal(3_745m, result.Total);
    }

    [Fact]
    public async Task PendingRowWithAStaleSystemBase_WhoseCurrentSourcePricesNothing_ShowsNoBaseLine()
    {
        var prior = await AddAppraisalAsync(_db, AppraisalTypes.Progressive, number: "AP-PRIOR");
        var priorAssignment = await AddAssignmentAsync(_db, prior.Id, assigned: true);
        await AddFeeAsync(_db, priorAssignment.Id); // no inspection fee keyed
        var (appraisalId, fee) = await SeedAsync(type: AppraisalTypes.Progressive, prevId: prior.Id);
        fee.AddItem("01", "old base", 9_000m);
        await _db.SaveChangesAsync();

        var result = await RunAsync(appraisalId);

        Assert.DoesNotContain(result.Items, i => i.FeeCode == "01");
        Assert.Equal(0m, result.Subtotal);
    }

    [Fact]
    public async Task AssignedRow_KeepsItsSystemBase_BecauseAssignmentWouldNotReprice()
    {
        var (appraisalId, fee) = await SeedAsync(sellingPrice: 8_000_000m, assigned: true);
        var stored = fee.AddItem("01", "system base", 2_500m);
        await _db.SaveChangesAsync();

        var result = await RunAsync(appraisalId);

        var baseLine = Assert.Single(result.Items, i => i.FeeCode == "01");
        Assert.Equal(stored.Id, baseLine.Id);
        Assert.Equal(2_500m, baseLine.Amount);
        Assert.Equal(2_500m, result.Subtotal);
    }

    [Fact]
    public async Task FeeWithoutASellingPrice_ShowsNoTierBase()
    {
        // The blank row an admin's first add creates: nothing would be charged for it by tier.
        SeedGenericLadder(_db);
        var appraisal = await AddAppraisalAsync(_db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(_db, appraisal.Id);
        await AddFeeAsync(_db, assignment.Id, sellingPrice: null, paymentType: null);

        var result = await RunAsync(appraisal.Id);

        Assert.Equal(FeeBaseKinds.None, result.BaseSource.Kind);
        Assert.Null(result.SystemBaseAmount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task IbgRequestRow_WithANullSellingPrice_ShowsTheTierBase_LikeAssignment()
    {
        SeedGenericLadder(_db);
        var appraisal = await AddAppraisalAsync(_db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(_db, appraisal.Id);
        await AddFeeAsync(_db, assignment.Id, sellingPrice: null, paymentType: "01"); // a request's row, IBG

        var result = await RunAsync(appraisal.Id);

        Assert.Equal(FeeBaseKinds.Tier, result.BaseSource.Kind);
        Assert.Equal(2_500m, result.SystemBaseAmount);
    }

    [Theory]
    [InlineData("quotation")]
    [InlineData("manual")]
    public async Task BlankAdminCreatedRow_ShowsNoBase_ForAnySource(string method)
    {
        var winner = Guid.NewGuid();
        SeedGenericLadder(_db);
        var appraisal = await AddAppraisalAsync(_db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(_db, appraisal.Id);
        var fee = await AddFeeAsync(_db, assignment.Id, sellingPrice: null, paymentType: null);
        fee.AddItem("02", "admin travel", 500m).MarkAsAdminSet();
        await _db.SaveChangesAsync();
        await AddFinalizedQuotationAsync(_db, appraisal.Id, winner, 4_500m);

        var result = await RunAsync(appraisal.Id, type: "external", method: method, companyId: winner);

        Assert.Equal(FeeBaseKinds.None, result.BaseSource.Kind);
        Assert.Null(result.SystemBaseAmount);
        Assert.Equal("02", Assert.Single(result.Items).FeeCode);
    }

    [Fact]
    public async Task NotYetCreatedShell_UsesTheDefaultVat_AsAppraisalFeeCreateWillGiveIt()
    {
        // FeeShellFactory copies the payment context from the newest fee but not its VAT rate: Create sets the default.
        SeedGenericLadder(_db);
        var appraisal = await AddAppraisalAsync(_db, AppraisalTypes.New);
        var older = await AddAssignmentAsync(_db, appraisal.Id, assigned: true, createdAt: new DateTime(2026, 1, 1));
        var oldFee = await AddFeeAsync(_db, older.Id, sellingPrice: 1_000_000m);
        typeof(AppraisalFee).GetProperty(nameof(AppraisalFee.VATRate))!.SetValue(oldFee, 5m);
        await AddAssignmentAsync(_db, appraisal.Id, assigned: false, createdAt: new DateTime(2026, 2, 1));
        await _db.SaveChangesAsync();

        var result = await RunAsync(appraisal.Id);

        Assert.Null(result.FeeId);
        Assert.Equal(AppraisalFee.DefaultVatRate, result.VatRate);
        Assert.Equal(175m, result.Vat); // 2,500 * 7%
    }

    [Fact]
    public async Task Quotation_WithTwoItemsForTheAppraisal_PicksTheSameOneAsTheConsumer()
    {
        var winner = Guid.NewGuid();
        var (appraisalId, _) = await SeedAsync();
        var rfq = await AddFinalizedQuotationAsync(_db, appraisalId, winner, 4_500m);
        var first = rfq.Quotations.Single().Items.Single();
        // A second item for the same appraisal, a different price, listed after the first by item number.
        var second = rfq.Quotations.Single().AddItem(Guid.NewGuid(), appraisalId, 9, 99m, 3);
        second.SetFeeBreakdown(7_000m, 0m, vatPercent: 7m);
        _db.Entry(second).Property("CreatedAt").CurrentValue = new DateTime(2026, 1, 1);
        _db.Entry(second).Property("CreatedBy").CurrentValue = "test";
        await _db.SaveChangesAsync();

        var preview = await RunAsync(appraisalId, type: "external", method: "quotation", companyId: winner);
        var stored = await _db.QuotationRequests.Include(q => q.Quotations).ThenInclude(c => c.Items).SingleAsync();
        var consumer = QuotationFeePrice.Resolve(stored, appraisalId, winner);

        Assert.Equal(consumer.Amount, preview.SystemBaseAmount);
        Assert.Equal(first.ExVatFee, preview.SystemBaseAmount); // the lower item number, in both
    }

    [Fact]
    public async Task AssignedRow_WithItemsButNoBase_ShowsNoWouldBeBase_LikeTheServicesHasItemsSafety()
    {
        // A re-fire on an assigned row never adds a base to a fee that already holds items.
        var (appraisalId, fee) = await SeedAsync(sellingPrice: 8_000_000m, assigned: true);
        fee.AddItem("02", "travel", 400m).MarkAsUserAdded();
        await _db.SaveChangesAsync();

        var result = await RunAsync(appraisalId);

        Assert.DoesNotContain(result.Items, i => i.FeeCode == "01");
        Assert.Equal(400m, result.Subtotal);
    }

    [Fact]
    public async Task AssignedRow_WithNoItems_StillShowsTheWouldBeBase()
    {
        var (appraisalId, _) = await SeedAsync(sellingPrice: 8_000_000m, assigned: true);

        var result = await RunAsync(appraisalId);

        Assert.Equal(3_000m, Assert.Single(result.Items, i => i.FeeCode == "01").Amount);
    }

    [Fact]
    public async Task Totals_UseTheAggregatesOwnBillableRuleAndVatArithmetic()
    {
        // Odd amounts: the preview must round exactly as AppraisalFee.RecalculateFromItems does.
        var (appraisalId, fee) = await SeedAsync(sellingPrice: 1_000_000m);
        fee.AddItem("01", "admin base", 1_234.55m).MarkAsAdminSet();
        fee.AddItem("02", "pending", 99m, requiresApproval: true).MarkAsUserAdded();
        fee.AddItem("03", "approved", 10.05m, requiresApproval: true).MarkAsUserAdded();
        fee.Items.Single(i => i.FeeCode == "03").Approve("P0001");
        fee.RecalculateFromItems();
        await _db.SaveChangesAsync();

        var result = await RunAsync(appraisalId);

        Assert.Equal(fee.TotalFeeBeforeVAT, result.Subtotal);
        Assert.Equal(fee.VATAmount, result.Vat);
        Assert.Equal(fee.TotalFeeAfterVAT, result.Total);
    }
}
