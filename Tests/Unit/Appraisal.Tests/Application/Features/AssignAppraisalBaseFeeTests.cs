using Appraisal.Application.Features.Assignments.AssignAppraisal;
using Appraisal.Application.Features.Fees.AddFeeItem;
using Appraisal.Application.Features.Fees.GetFeePreview;
using Appraisal.Application.Features.Quotations.AutoAssign;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Quotations;
using Appraisal.Infrastructure;
using Auth.Contracts.Companies;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shared.Exceptions;
using Shared.Identity;
using Workflow;
using static Appraisal.Tests.Application.Services.FeeTestSeed;

namespace Appraisal.Tests.Application.Features;

/// <summary>
/// Assigning must be refused (400, errorCode FeeBaseMissing) when it would bill no base fee (01): for every type and
/// method, judged by the real fee preview (the same resolution AssignmentFeeService materialises) over a real
/// in-memory fee graph. The admin's own 01 above 0 rescues it; reject / request-more-info are not asked.
/// </summary>
public class AssignAppraisalBaseFeeTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly AppraisalDbContext _db = NewDb();
    private readonly IQuotationRepository _quotations = Substitute.For<IQuotationRepository>();
    private readonly IAppraisalRepository _appraisals = Substitute.For<IAppraisalRepository>();
    private readonly IQuotationAutoAssignStateReader _ownership = Substitute.For<IQuotationAutoAssignStateReader>();
    private readonly IWorkflowRelayService _relay = Substitute.For<IWorkflowRelayService>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly Guid _winner = Guid.NewGuid();

    public AssignAppraisalBaseFeeTests()
    {
        _user.UserCode.Returns("P0001");
        _ownership.WorkflowInstanceBelongsToAppraisalAsync(default, default, default).ReturnsForAnyArgs(true);
        _quotations.HasFinalizedWonByAsync(default, default, default).ReturnsForAnyArgs(true);
        // The handler asks the real preview handler, exactly as MediatR would route it.
        _sender.Send(Arg.Any<GetFeePreviewQuery>(), Arg.Any<CancellationToken>()).Returns(call =>
            new GetFeePreviewQueryHandler(_db, BuildService(_db), Substitute.For<ICompanyLookupService>())
                .Handle(call.Arg<GetFeePreviewQuery>(), call.Arg<CancellationToken>()));
    }

    private async Task<(Guid AppraisalId, AppraisalFee? Fee)> SeedAsync(
        bool ladder = true, bool feeRow = true, string? paymentType = DefaultPaymentType)
    {
        if (ladder) SeedGenericLadder(_db);
        var appraisal = await AddAppraisalAsync(_db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(_db, appraisal.Id);
        var fee = feeRow ? await AddFeeAsync(_db, assignment.Id, paymentType is null ? null : 1_000_000m, paymentType: paymentType) : null;
        _appraisals.GetByIdWithAllDataAsync(appraisal.Id, Arg.Any<CancellationToken>())
            .Returns(_ => _db.Appraisals.Include(a => a.Assignments).First(a => a.Id == appraisal.Id));
        return (appraisal.Id, fee);
    }

    private async Task AssignAsync(Guid appraisalId, string decision, string method = "Manual")
    {
        var handler = new AssignAppraisalCommandHandler(
            _appraisals, Substitute.For<IAppraisalUnitOfWork>(), _relay, _quotations, _ownership, _sender, _user);
        await handler.Handle(
            new AssignAppraisalCommand(appraisalId, Guid.NewGuid(), AssigneeCompanyId: _winner.ToString(),
                AssignmentMethod: method, DecisionTaken: decision), Ct);
    }

    private static void ResumeNever(IWorkflowRelayService relay) =>
        relay.DidNotReceiveWithAnyArgs().ResumeWorkflowAsync(default, default!, default!, default!, default, default);

    private Task Resumed() => _relay.Received(1).ResumeWorkflowAsync(
        Arg.Any<Guid>(), "appraisal-assignment", Arg.Any<string>(), Arg.Any<Dictionary<string, object>>(),
        Arg.Any<IReadOnlyDictionary<string, WorkflowAssigneeOverride>?>(), Arg.Any<CancellationToken>());

    private async Task AssertRefusedAsync(Guid appraisalId, string decision, string method = "Manual")
    {
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => AssignAsync(appraisalId, decision, method));
        Assert.Equal("FeeBaseMissing", ex.Code);
        Assert.Contains("base appraisal fee (01)", ex.Message);
        ResumeNever(_relay);
        // Refused before anything is recorded: the previous assigner is untouched.
        Assert.Equal("test", (await _db.AppraisalAssignments.SingleAsync(Ct)).AssignedBy);
    }

    // internal, external, external-offline, and "proceed"
    [Theory]
    [InlineData("INT")]
    [InlineData("EXT")]
    [InlineData("EXTO")]
    [InlineData("P")]
    public async Task With_a_tier_base_every_type_is_assigned(string decision)
    {
        var (appraisalId, _) = await SeedAsync();

        await AssignAsync(appraisalId, decision);

        await Resumed();
    }

    [Theory]
    [InlineData("INT")]
    [InlineData("EXT")]
    [InlineData("EXTO")]
    [InlineData("P")]
    public async Task Without_any_fee_row_every_type_is_refused(string decision)
    {
        var (appraisalId, _) = await SeedAsync(feeRow: false);

        await AssertRefusedAsync(appraisalId, decision);
    }

    [Theory]
    [InlineData("INT")]
    [InlineData("EXT")]
    [InlineData("EXTO")]
    public async Task A_blank_shell_prices_nothing_so_it_is_refused(string decision)
    {
        var (appraisalId, _) = await SeedAsync(paymentType: null);

        await AssertRefusedAsync(appraisalId, decision);
    }

    [Theory]
    [InlineData("INT")]
    [InlineData("EXT")]
    [InlineData("EXTO")]
    public async Task A_tier_ladder_with_no_band_gives_no_base_so_it_is_refused(string decision)
    {
        var (appraisalId, _) = await SeedAsync(ladder: false);

        await AssertRefusedAsync(appraisalId, decision);
    }

    [Theory]
    [InlineData("INT")]
    [InlineData("EXT")]
    [InlineData("EXTO")]
    public async Task The_admins_own_base_above_zero_rescues_a_blank_shell_with_no_tier(string decision)
    {
        var (appraisalId, fee) = await SeedAsync(ladder: false, paymentType: null);
        fee!.AddItem("01", "admin base", 4_000m).MarkAsAdminSet();
        await _db.SaveChangesAsync(Ct);

        await AssignAsync(appraisalId, decision);

        await Resumed();
    }

    [Fact]
    public async Task An_admin_base_of_zero_is_not_a_base()
    {
        var (appraisalId, fee) = await SeedAsync();
        fee!.AddItem("01", "admin base", 0m).MarkAsAdminSet();
        await _db.SaveChangesAsync(Ct);

        await AssertRefusedAsync(appraisalId, "INT");
    }

    [Fact]
    public async Task A_non_base_admin_item_does_not_count()
    {
        var (appraisalId, fee) = await SeedAsync(ladder: false);
        fee!.AddItem("02", "admin travel", 500m).MarkAsAdminSet();
        await _db.SaveChangesAsync(Ct);

        await AssertRefusedAsync(appraisalId, "INT");
    }

    [Fact]
    public async Task A_quotation_winner_with_a_price_is_assigned_even_with_no_tier()
    {
        var (appraisalId, _) = await SeedAsync(ladder: false);
        await AddFinalizedQuotationAsync(_db, appraisalId, _winner, feeAmount: 9_000m);

        await AssignAsync(appraisalId, "EXT", "Quotation");

        await Resumed();
    }

    [Theory]
    [InlineData("EXT")]
    [InlineData("P")]
    public async Task An_unpriced_quotation_winner_is_refused_even_though_the_tier_ladder_exists(string decision)
    {
        // A quotation assignment is billed from the quotation, never from the tier.
        var (appraisalId, _) = await SeedAsync();
        await AddFinalizedQuotationAsync(_db, appraisalId, _winner, feeAmount: 0m);

        await AssertRefusedAsync(appraisalId, decision, "Quotation");
    }

    [Fact]
    public async Task An_unpriced_quotation_winner_is_rescued_by_the_admins_own_base()
    {
        var (appraisalId, fee) = await SeedAsync();
        await AddFinalizedQuotationAsync(_db, appraisalId, _winner, feeAmount: 0m);
        fee!.AddItem("01", "admin base", 4_000m).MarkAsAdminSet();
        await _db.SaveChangesAsync(Ct);

        await AssignAsync(appraisalId, "EXT", "Quotation");

        await Resumed();
    }

    // A User 01 only counts once billable: awaiting approval or rejected, it bills nothing, so Assign is refused
    // and the preview says so (hasBillableBase) — the one rule the screen and the guard share.
    private async Task<(Guid AppraisalId, AppraisalFeeItem Item)> SeedUserBaseAsync()
    {
        var (appraisalId, fee) = await SeedAsync(ladder: false);
        var item = fee!.AddItem("01", "user base", 4_000m);
        item.MarkAsUserAdded();
        item.MakePendingApproval();
        item.MarkApprovalSubmitted();
        await _db.SaveChangesAsync(Ct);
        return (appraisalId, item);
    }

    private async Task<bool> HasBillableBaseAsync(Guid appraisalId) =>
        (await _sender.Send(new GetFeePreviewQuery(appraisalId, "internal", "manual", null), Ct)).HasBillableBase;

    [Fact]
    public async Task A_user_base_awaiting_approval_is_not_billable_so_assign_is_refused()
    {
        var (appraisalId, _) = await SeedUserBaseAsync();

        Assert.False(await HasBillableBaseAsync(appraisalId));
        await AssertRefusedAsync(appraisalId, "INT");
    }

    [Fact]
    public async Task A_rejected_user_base_is_not_billable_so_assign_is_refused()
    {
        var (appraisalId, item) = await SeedUserBaseAsync();
        item.Reject("P5229", "no");
        await _db.SaveChangesAsync(Ct);

        Assert.False(await HasBillableBaseAsync(appraisalId));
        await AssertRefusedAsync(appraisalId, "INT");
    }

    [Fact]
    public async Task An_approved_user_base_is_billable_so_assign_goes_through()
    {
        var (appraisalId, item) = await SeedUserBaseAsync();
        item.Approve("P5229");
        await _db.SaveChangesAsync(Ct);

        Assert.True(await HasBillableBaseAsync(appraisalId));
        await AssignAsync(appraisalId, "INT");
        await Resumed();
    }

    [Fact]
    public async Task HasBillableBase_is_true_for_an_admin_or_system_base_above_zero_and_false_at_zero()
    {
        var (adminId, adminFee) = await SeedAsync(ladder: false);
        adminFee!.AddItem("01", "admin base", 4_000m).MarkAsAdminSet();
        var (zeroId, zeroFee) = await SeedAsync(ladder: false);
        zeroFee!.AddItem("01", "admin base", 0m).MarkAsAdminSet();
        var (systemId, _) = await SeedAsync(); // tier ladder: the would-be system base
        await _db.SaveChangesAsync(Ct);

        Assert.True(await HasBillableBaseAsync(adminId));
        Assert.False(await HasBillableBaseAsync(zeroId));
        Assert.True(await HasBillableBaseAsync(systemId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_shell_patched_with_an_empty_payment_type_stays_blank_and_bills_no_base(string sent)
    {
        // The screen sends '' for a stored null. It must not turn the shell into a priced one (tier at price 0).
        var (appraisalId, fee) = await SeedAsync(paymentType: null);

        await new Appraisal.Application.Features.Fees.UpdateFee.UpdateFeeCommandHandler(_db)
            .Handle(new Appraisal.Application.Features.Fees.UpdateFee.UpdateFeeCommand(fee!.Id, sent, 0m), Ct);
        await _db.SaveChangesAsync(Ct);

        Assert.True(fee.IsBlank);
        Assert.Null(fee.FeePaymentType);
        var preview = await _sender.Send(new GetFeePreviewQuery(appraisalId, "internal", "manual", null), Ct);
        Assert.False(preview.HasBillableBase);
        Assert.Null(preview.SystemBaseAmount);
        await AssertRefusedAsync(appraisalId, "INT");
    }

    [Fact]
    public async Task A_routed_back_row_with_a_stale_system_base_and_an_unpriced_source_can_be_unstuck_by_the_admin()
    {
        // After a route-back the stored System 01 is hidden by the preview (the unpriced quotation prices nothing),
        // so no 01 shows, Assign is refused, and the admin must still be able to key a base.
        var (appraisalId, fee) = await SeedAsync();
        fee!.AddItem("01", "system base", 2_500m);
        await AddFinalizedQuotationAsync(_db, appraisalId, _winner, feeAmount: 0m);
        await _db.SaveChangesAsync(Ct);
        await AssertRefusedAsync(appraisalId, "EXT", "Quotation");

        var added = await new AddFeeItemCommandHandler(_db, Substitute.For<ISender>(), _user)
            .Handle(new AddFeeItemCommand(appraisalId, fee.Id, "01", "admin base", 4_000m), Ct);
        await _db.SaveChangesAsync(Ct);

        var preview = await _sender.Send(new GetFeePreviewQuery(appraisalId, "external", "quotation", _winner), Ct);
        var line = Assert.Single(preview.Items, i => i.FeeCode == "01");
        Assert.Equal((added.ItemId, "Admin", 4_000m), (line.Id, line.Source, line.Amount));
        await AssignAsync(appraisalId, "EXT", "Quotation");
        await Resumed();
    }

    // Reject (C) and request-more-info (R) assign nobody, so they never need a fee.
    [Theory]
    [InlineData("C")]
    [InlineData("R")]
    public async Task Reject_and_request_more_info_are_not_asked(string decision)
    {
        var (appraisalId, _) = await SeedAsync(feeRow: false);

        await AssignAsync(appraisalId, decision);

        await Resumed();
        await _sender.DidNotReceiveWithAnyArgs().Send(Arg.Any<GetFeePreviewQuery>(), default);
    }
}
