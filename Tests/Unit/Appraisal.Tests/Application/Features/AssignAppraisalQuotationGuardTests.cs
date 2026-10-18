using Appraisal.Application.Features.Assignments.AssignAppraisal;
using Appraisal.Application.Features.Quotations.AutoAssign;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Quotations;
using Appraisal.Infrastructure;
using Appraisal.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shared.Exceptions;
using Workflow;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Application.Features;

/// <summary>
/// A "Quotation" assignment is billed from the quotation the company won, by a consumer that runs after the
/// workflow resume. The handler must refuse a non-winner BEFORE resuming, or the case is left stuck.
/// </summary>
public class AssignAppraisalQuotationGuardTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IQuotationRepository _quotations = Substitute.For<IQuotationRepository>();
    private readonly IAppraisalRepository _appraisals = Substitute.For<IAppraisalRepository>();
    private readonly IQuotationAutoAssignStateReader _ownership = Substitute.For<IQuotationAutoAssignStateReader>();
    private readonly IWorkflowRelayService _relay = Substitute.For<IWorkflowRelayService>();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly AppraisalAggregate _appraisal;

    public AssignAppraisalQuotationGuardTests()
    {
        _appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
        _appraisal.AssignAdmin();
        // By default the command's workflow instance belongs to the appraisal.
        _ownership.WorkflowInstanceBelongsToAppraisalAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    private AssignAppraisalCommandHandler Handler()
    {
        _appraisals.GetByIdWithAllDataAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_appraisal);
        return new AssignAppraisalCommandHandler(_appraisals, Substitute.For<IAppraisalUnitOfWork>(), _relay, _quotations, _ownership);
    }

    // company: omitted -> the winner's id; pass null / "" / garbage explicitly to send exactly that.
    private AssignAppraisalCommand Command(string method, string decision = "EXT", string? company = "<winner>") =>
        new(_appraisal.Id, Guid.NewGuid(), AssigneeCompanyId: company == "<winner>" ? _companyId.ToString() : company,
            AssignmentMethod: method, AssignedBy: "P0001", DecisionTaken: decision);

    private Task ResumeCalls() => _relay.Received(1).ResumeWorkflowAsync(
        Arg.Any<Guid>(), "appraisal-assignment", Arg.Any<string>(), Arg.Any<Dictionary<string, object>>(),
        Arg.Any<IReadOnlyDictionary<string, WorkflowAssigneeOverride>?>(), Arg.Any<CancellationToken>());

    [Fact]
    public async Task Winner_of_a_finalized_quotation_is_assigned()
    {
        _quotations.HasFinalizedWonByAsync(_appraisal.Id, _companyId, Arg.Any<CancellationToken>()).Returns(true);

        await Handler().Handle(Command("Quotation"), Ct);

        await ResumeCalls();
    }

    [Theory]
    [InlineData("Quotation", "EXT")]
    [InlineData("quotation", "ext")]
    [InlineData("Quotation", "P")]    // "proceed" also reaches company selection (admin-proceed-external)
    [InlineData("Quotation", "p")]
    public async Task Non_winner_is_a_400_before_the_workflow_resumes(string method, string decision)
    {
        // The repository finds no finalized, priced quotation won by this company.
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => Handler().Handle(Command(method, decision), Ct));

        Assert.Equal("The selected company did not win a finalized quotation with a price for this appraisal.", ex.Message);
        await _quotations.Received(1).HasFinalizedWonByAsync(_appraisal.Id, _companyId, Arg.Any<CancellationToken>());
        await _relay.DidNotReceiveWithAnyArgs().ResumeWorkflowAsync(default, default!, default!, default!, default, default);
    }

    [Theory]
    [InlineData("EXT", null)]
    [InlineData("EXT", "")]
    [InlineData("EXT", "not-a-guid")]
    [InlineData("P", null)]
    [InlineData("P", "not-a-guid")]
    public async Task Missing_or_unparsable_company_is_a_400_without_asking_the_repository(string decision, string? company)
    {
        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => Handler().Handle(Command("Quotation", decision, company), Ct));

        Assert.Equal("A company is required for a Quotation assignment.", ex.Message);
        // The TryParse branch fired, so the repository was never reached.
        await _quotations.DidNotReceiveWithAnyArgs().HasFinalizedWonByAsync(default, default, default);
        await _relay.DidNotReceiveWithAnyArgs().ResumeWorkflowAsync(default, default!, default!, default!, default, default);
    }

    // Reject (C) and request-more-info (R) never reach company selection, so a non-winner must not block them.
    [Theory]
    [InlineData("C")]
    [InlineData("R")]
    public async Task Reject_and_request_more_info_are_not_guarded_for_the_Quotation_method(string decision)
    {
        await Handler().Handle(Command("Quotation", decision), Ct);

        await _quotations.DidNotReceiveWithAnyArgs().HasFinalizedWonByAsync(default, default, default);
        await ResumeCalls();
    }

    [Fact]
    public async Task Ownership_is_checked_before_the_quotation_guard_so_it_cannot_be_probed_for_foreign_appraisals()
    {
        _ownership.WorkflowInstanceBelongsToAppraisalAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => Handler().Handle(Command("Quotation"), Ct));

        Assert.Equal("Workflow instance does not belong to this appraisal", ex.Message);
        await _quotations.DidNotReceiveWithAnyArgs().HasFinalizedWonByAsync(default, default, default);
    }

    [Fact]
    public async Task Winner_without_a_usable_price_for_this_appraisal_is_a_400_and_nothing_resumes()
    {
        // The repository only says "yes" for a winner priced for THIS appraisal (see the in-memory test below).
        _quotations.HasFinalizedWonByAsync(_appraisal.Id, _companyId, Arg.Any<CancellationToken>()).Returns(false);

        await Assert.ThrowsAsync<BadRequestException>(() => Handler().Handle(Command("Quotation"), Ct));

        await _relay.DidNotReceiveWithAnyArgs().ResumeWorkflowAsync(default, default!, default!, default!, default, default);
    }

    // Exercises the real HasFinalizedWonByAsync predicate against the in-memory provider (evaluates the LINQ in C#;
    // SQL Server translation of the same expression is not covered here).
    // fee/discount/negotiated (0 = none): breakdown item (FeeAfterDiscount); quoted: legacy QuotedPrice; null item = no row.
    [Theory]
    [InlineData(1000, 100, 0, 0, true)]          // breakdown, ex-VAT after discount = 900
    [InlineData(1000, 100, 50, 0, true)]         // breakdown with negotiated discount = 850
    [InlineData(0, 0, 0, 500, true)]             // legacy row: QuotedPrice only
    [InlineData(1000, 1000, 0, 0, false)]     // breakdown fully discounted -> 0
    [InlineData(1000, 100, 900, 0, false)]       // negotiated discount takes it to 0
    [InlineData(0, 0, 0, 0, false)]              // unpriced item
    public async Task Repository_requires_the_winner_to_have_a_usable_price_for_the_appraisal(
        decimal fee, decimal discount, decimal negotiated, decimal quoted, bool expected)
    {
        await using var db = NewDb();
        var winner = Finalize(db, _appraisal.Id, _companyId, out _);
        var item = winner.AddItem(Guid.NewGuid(), _appraisal.Id, 1, quoted, 3);
        if (fee > 0m) item.SetFeeBreakdown(fee, discount, 7m);
        if (negotiated > 0m) item.SetNegotiatedDiscount(negotiated, "n/a");
        await db.SaveChangesAsync(Ct);

        Assert.Equal(expected, await new QuotationRepository(db).HasFinalizedWonByAsync(_appraisal.Id, _companyId, Ct));
    }

    [Fact]
    public async Task Repository_rejects_a_winner_with_no_item_or_only_an_item_for_another_appraisal()
    {
        await using var db = NewDb();
        var winner = Finalize(db, _appraisal.Id, _companyId, out _);
        await db.SaveChangesAsync(Ct);
        var repository = new QuotationRepository(db);

        Assert.False(await repository.HasFinalizedWonByAsync(_appraisal.Id, _companyId, Ct)); // no item at all

        winner.AddItem(Guid.NewGuid(), Guid.NewGuid(), 1, 50_000m, 3); // priced, but for another appraisal
        await db.SaveChangesAsync(Ct);

        Assert.False(await repository.HasFinalizedWonByAsync(_appraisal.Id, _companyId, Ct));
    }

    [Fact]
    public async Task Repository_requires_every_item_for_the_appraisal_to_be_priced()
    {
        await using var db = NewDb();
        var winner = Finalize(db, _appraisal.Id, _companyId, out _);
        winner.AddItem(Guid.NewGuid(), _appraisal.Id, 1, 500m, 3);
        winner.AddItem(Guid.NewGuid(), _appraisal.Id, 2, 0m, 3); // the fee consumer may pick either one
        await db.SaveChangesAsync(Ct);

        Assert.False(await new QuotationRepository(db).HasFinalizedWonByAsync(_appraisal.Id, _companyId, Ct));
    }

    // The guard must judge the SAME quotation the fee consumer uses: the latest finalized one (SelectedAt DESC).
    [Theory]
    [InlineData(true, false, false)]  // older RFQ priced, newer one unpriced -> consumer would fail -> refuse
    [InlineData(false, true, true)]   // older unpriced, newer priced -> consumer succeeds -> allow
    public async Task Repository_judges_only_the_latest_finalized_quotation_won_by_the_company(
        bool olderPriced, bool newerPriced, bool expected)
    {
        await using var db = NewDb();
        var older = Finalize(db, _appraisal.Id, _companyId, out _, new DateTime(2026, 10, 1));
        var newer = Finalize(db, _appraisal.Id, _companyId, out _, new DateTime(2026, 10, 15));
        if (olderPriced) older.AddItem(Guid.NewGuid(), _appraisal.Id, 1, 500m, 3);
        if (newerPriced) newer.AddItem(Guid.NewGuid(), _appraisal.Id, 1, 500m, 3);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(expected, await new QuotationRepository(db).HasFinalizedWonByAsync(_appraisal.Id, _companyId, Ct));
    }

    // The current round decides: Q0 (older) was won by X, Q1 (newer) by Y. Only the latest RFQ's winner may be assigned.
    [Fact]
    public async Task Repository_refuses_a_company_that_only_won_a_superseded_round()
    {
        var companyY = Guid.NewGuid();
        await using var db = NewDb();
        var q0Winner = Finalize(db, _appraisal.Id, _companyId, out _, new DateTime(2026, 10, 1));
        var q1Winner = Finalize(db, _appraisal.Id, companyY, out var q1, new DateTime(2026, 10, 15));
        q0Winner.AddItem(Guid.NewGuid(), _appraisal.Id, 1, 500m, 3);
        q1Winner.AddItem(Guid.NewGuid(), _appraisal.Id, 1, 700m, 3);
        await db.SaveChangesAsync(Ct);
        var repository = new QuotationRepository(db);

        Assert.False(await repository.HasFinalizedWonByAsync(_appraisal.Id, _companyId, Ct)); // X: old round only
        Assert.True(await repository.HasFinalizedWonByAsync(_appraisal.Id, companyY, Ct));
        Assert.Null(await repository.GetFinalizedByAppraisalIdAsync(_appraisal.Id, _companyId, Ct));
        Assert.Equal(q1.Id, (await repository.GetFinalizedByAppraisalIdAsync(_appraisal.Id, companyY, Ct))!.Id);
    }

    [Fact]
    public async Task Repository_requires_the_company_own_quotation_to_be_the_winner_not_just_SelectedCompanyId()
    {
        var companyY = Guid.NewGuid();
        await using var db = NewDb();
        // RFQ says X (_companyId) was selected, but only Y's quotation carries IsWinner. X's is priced and not a winner.
        var xQuotation = Finalize(db, _appraisal.Id, _companyId, out var rfq, markWinner: false);
        xQuotation.AddItem(Guid.NewGuid(), _appraisal.Id, 1, 500m, 3);
        var yQuotation = CompanyQuotation.Create(rfq.Id, Guid.NewGuid(), companyY, "QT-Y", DateTime.Now);
        typeof(CompanyQuotation).BaseType!.GetProperty("Id")!.SetValue(yQuotation, Guid.NewGuid());
        yQuotation.AddItem(Guid.NewGuid(), _appraisal.Id, 1, 700m, 3);
        yQuotation.MarkAsWinner();
        rfq.AddQuotation(yQuotation);
        await db.SaveChangesAsync(Ct);

        Assert.False(await new QuotationRepository(db).HasFinalizedWonByAsync(_appraisal.Id, _companyId, Ct));
    }

    private static AppraisalDbContext NewDb() => new(new DbContextOptionsBuilder<AppraisalDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    // A Finalized RFQ for the appraisal, won by the company; the winning quotation has no items yet.
    private static CompanyQuotation Finalize(AppraisalDbContext db, Guid appraisalId, Guid companyId, out QuotationRequest rfq,
        DateTime? selectedAt = null, bool markWinner = true)
    {
        rfq = QuotationRequest.Create(DateTime.Now.AddDays(1), "admin", DateTime.Now);
        rfq.AddAppraisal(appraisalId, "admin", DateTime.Now);
        var winner = CompanyQuotation.Create(rfq.Id, Guid.NewGuid(), companyId, "QT-001", DateTime.Now);
        // Id is server-generated in production; the in-memory provider needs it before items reference it.
        typeof(CompanyQuotation).BaseType!.GetProperty("Id")!.SetValue(winner, Guid.NewGuid());
        if (markWinner) winner.MarkAsWinner();
        rfq.AddQuotation(winner);
        typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.Status))!.SetValue(rfq, "Finalized");
        typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.SelectedCompanyId))!.SetValue(rfq, companyId);
        typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.SelectedAt))!.SetValue(rfq, selectedAt ?? DateTime.Now);
        db.QuotationRequests.Add(rfq);
        return winner;
    }

    [Theory]
    [InlineData("Manual", "EXT")]
    [InlineData("RoundRobin", "EXT")]
    [InlineData("Quotation", "INT")]
    [InlineData("Quotation", "EXTO")]
    public async Task Other_methods_and_routes_that_name_no_company_are_not_checked(string method, string decision)
    {
        await Handler().Handle(Command(method, decision), Ct);

        await _quotations.DidNotReceiveWithAnyArgs().HasFinalizedWonByAsync(default, default, default);
        await ResumeCalls();
    }

    [Theory]
    [InlineData("Manual")]
    [InlineData("RoundRobin")]
    [InlineData("Quotation")]
    public async Task Workflow_instance_of_another_appraisal_is_a_400_for_every_method_and_nothing_resumes(string method)
    {
        _quotations.HasFinalizedWonByAsync(_appraisal.Id, _companyId, Arg.Any<CancellationToken>()).Returns(true);
        _ownership.WorkflowInstanceBelongsToAppraisalAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => Handler().Handle(Command(method), Ct));

        Assert.Equal("Workflow instance does not belong to this appraisal", ex.Message);
        await _relay.DidNotReceiveWithAnyArgs().ResumeWorkflowAsync(default, default!, default!, default!, default, default);
    }

    [Fact]
    public async Task Ownership_is_checked_for_the_commands_own_appraisal_and_workflow_instance()
    {
        var command = Command("Manual");

        await Handler().Handle(command, Ct);

        await _ownership.Received(1).WorkflowInstanceBelongsToAppraisalAsync(
            _appraisal.Id, command.WorkflowInstanceId, Arg.Any<CancellationToken>());
    }
}
