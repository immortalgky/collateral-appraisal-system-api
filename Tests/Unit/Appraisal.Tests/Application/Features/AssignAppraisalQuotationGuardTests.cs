using Appraisal.Application.Features.Assignments.AssignAppraisal;
using Appraisal.Application.Features.Fees.GetFeePreview;
using Appraisal.Application.Features.Quotations.AutoAssign;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Quotations;
using Appraisal.Infrastructure;
using Appraisal.Infrastructure.Repositories;
using Auth.Contracts.Companies;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shared.Exceptions;
using Shared.Identity;
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
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly ICompanyLookupService _companies = Substitute.For<ICompanyLookupService>();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly AppraisalAggregate _appraisal;

    public AssignAppraisalQuotationGuardTests()
    {
        _appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
        _appraisal.AssignAdmin();
        _user.UserCode.Returns("P0001");
        // EXTO names a company: by default it exists and is within its MOU window.
        _companies.GetByIdAsync(_companyId, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(_companyId, "Acme", null, IsAssignable: true));
        // By default the fee preview shows a base fee (01), so these tests are about the quotation guard.
        _sender.Send(Arg.Any<GetFeePreviewQuery>(), Arg.Any<CancellationToken>())
            .Returns(FeePreviewWithBase(2_500m));
        // By default the command's workflow instance belongs to the appraisal.
        _ownership.WorkflowInstanceBelongsToAppraisalAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    internal static FeePreviewResult FeePreviewWithBase(decimal? amount) => new(
        null, amount, new FeeBaseSourceDto(FeeBaseKinds.Tier, null, null, null, null, null, null),
        null, amount is { } a ? [new FeePreviewItemDto(null, "01", "base", a, "System")] : [], amount > 0m, amount ?? 0m, 7m, 0m, amount ?? 0m);

    private AssignAppraisalCommandHandler Handler()
    {
        _appraisals.GetByIdWithAllDataAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_appraisal);
        return new AssignAppraisalCommandHandler(_appraisals, Substitute.For<IAppraisalUnitOfWork>(), _relay, _quotations, _ownership, _sender, _user, _companies);
    }

    // company: omitted -> the winner's id; pass null / "" / garbage explicitly to send exactly that.
    private AssignAppraisalCommand Command(string method, string decision = "EXT", string? company = "<winner>") =>
        new(_appraisal.Id, Guid.NewGuid(), AssigneeCompanyId: company == "<winner>" ? _companyId.ToString() : company,
            AssignmentMethod: method, DecisionTaken: decision);

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

    [Fact]
    public async Task The_admin_who_assigns_is_recorded_on_the_row_before_the_workflow_resumes()
    {
        // The consumers that later call Assign() are workflow-driven and used to stamp "System" over the admin.
        _appraisals.GetByIdWithAllDataAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_appraisal);
        var uow = Substitute.For<IAppraisalUnitOfWork>();
        var assignedByWhenResumed = "";
        _relay.ResumeWorkflowAsync(default, default!, default!, default!, default, default)
            .ReturnsForAnyArgs(_ =>
            {
                assignedByWhenResumed = _appraisal.Assignments.Single().AssignedBy;
                return Task.CompletedTask;
            });
        var handler = new AssignAppraisalCommandHandler(_appraisals, uow, _relay, _quotations, _ownership, _sender, _user, _companies);
        _user.UserCode.Returns("P5229");

        // The authenticated user is who is recorded, and who completes the task.
        await handler.Handle(Command("Manual", "INT"), Ct);

        Assert.Equal("P5229", assignedByWhenResumed);
        await _relay.Received(1).ResumeWorkflowAsync(
            Arg.Any<Guid>(), "appraisal-assignment", "P5229", Arg.Any<Dictionary<string, object>>(),
            Arg.Any<IReadOnlyDictionary<string, WorkflowAssigneeOverride>?>(), Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>()); // durable before the resume
    }

    // Reject (C) and request-more-info (R) assign nobody, so the admin must not be credited as the assigner.
    [Theory]
    [InlineData("C")]
    [InlineData("R")]
    public async Task Reject_and_request_more_info_do_not_record_the_admin_as_the_assigner(string decision)
    {
        _user.UserCode.Returns("P5229");

        await Handler().Handle(Command("Manual", decision), Ct);

        Assert.Equal("System", _appraisal.Assignments.Single().AssignedBy);
        await ResumeCalls();
    }

    [Theory]
    [InlineData("EXT")]
    [InlineData("INT")]
    [InlineData("EXTO")]
    [InlineData("P")]
    public async Task Assigning_decisions_record_the_admin_as_the_assigner(string decision)
    {
        _user.UserCode.Returns("P5229");

        await Handler().Handle(Command("Manual", decision), Ct);

        Assert.Equal("P5229", _appraisal.Assignments.Single().AssignedBy);
    }

    [Fact]
    public async Task The_trimmed_user_code_is_what_the_row_the_resume_and_the_override_all_carry()
    {
        _user.UserCode.Returns("  P5229 ");

        await Handler().Handle(Command("Manual", "INT") with { AssigneeUserId = "int.staff1" }, Ct);

        Assert.Equal("P5229", _appraisal.Assignments.Single().AssignedBy);
        await _relay.Received(1).ResumeWorkflowAsync(
            Arg.Any<Guid>(), "appraisal-assignment", "P5229", Arg.Any<Dictionary<string, object>>(),
            Arg.Is<IReadOnlyDictionary<string, WorkflowAssigneeOverride>?>(o =>
                o != null && o["int-appraisal-execution"].OverrideBy == "P5229"),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_user_code_is_refused_like_a_missing_one(string code)
    {
        _user.UserCode.Returns(code);

        await Assert.ThrowsAsync<ForbiddenException>(() => Handler().Handle(Command("Manual", "INT"), Ct));

        await _relay.DidNotReceiveWithAnyArgs().ResumeWorkflowAsync(default, default!, default!, default!, default, default);
        Assert.Equal("System", _appraisal.Assignments.Single().AssignedBy);
    }

    [Fact]
    public async Task The_assigner_is_stamped_on_the_current_pending_row_not_an_older_one()
    {
        // Two live rows: an older Pending one and the newest (also Pending). The consumers and the fee guard act on the
        // newest, so that is the row the admin must be recorded on.
        var older = _appraisal.Assignments.Single();
        older.CreatedAt = new DateTime(2026, 1, 1);
        var newer = _appraisal.AssignAdmin();
        newer.CreatedAt = new DateTime(2026, 2, 1);
        _user.UserCode.Returns("P5229");

        var result = await Handler().Handle(Command("Manual", "INT") with { Remark = "r" }, Ct);

        Assert.Equal(newer.Id, result.AssignmentId);
        Assert.Equal("P5229", newer.AssignedBy);
        Assert.Equal("r", newer.Remark);
        Assert.Equal("System", older.AssignedBy);
        Assert.Null(older.Remark);
    }

    [Fact]
    public async Task When_the_newest_live_row_is_not_pending_an_older_pending_one_is_not_acted_on()
    {
        var older = _appraisal.Assignments.Single();
        older.CreatedAt = new DateTime(2026, 1, 1);
        var newer = _appraisal.AssignAdmin();
        newer.CreatedAt = new DateTime(2026, 2, 1);
        newer.Assign("Internal", assigneeUserId: "P0001", assignedBy: "P0009");

        await Assert.ThrowsAsync<BadRequestException>(() => Handler().Handle(Command("Manual", "INT"), Ct));

        Assert.Equal("System", older.AssignedBy);
    }

    [Fact]
    public async Task Without_an_authenticated_user_nothing_is_recorded_or_resumed()
    {
        _user.UserCode.Returns((string?)null);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            Handler().Handle(Command("Manual", "INT"), Ct));
        Assert.Contains("could not be determined", ex.Message);

        await _relay.DidNotReceiveWithAnyArgs().ResumeWorkflowAsync(default, default!, default!, default!, default, default);
        Assert.Equal("System", _appraisal.Assignments.Single().AssignedBy);
    }

    [Fact]
    public async Task A_winner_without_a_price_is_refused_by_the_base_fee_rule_not_by_the_winner_check()
    {
        // The winner check only asks "did they win"; the unusable price shows up as no base (01) in the preview.
        _quotations.HasFinalizedWonByAsync(_appraisal.Id, _companyId, Arg.Any<CancellationToken>()).Returns(true);
        _sender.Send(Arg.Any<GetFeePreviewQuery>(), Arg.Any<CancellationToken>()).Returns(FeePreviewWithBase(null));

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => Handler().Handle(Command("Quotation"), Ct));

        Assert.Equal(AssignAppraisalCommandHandler.FeeBaseMissingCode, ex.Code);
        await _relay.DidNotReceiveWithAnyArgs().ResumeWorkflowAsync(default, default!, default!, default!, default, default);
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

        Assert.Equal("The selected company did not win a finalized quotation for this appraisal.", ex.Message);
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
    public async Task Repository_accepts_an_unpriced_winner_but_not_another_company()
    {
        await using var db = NewDb();
        var winner = Finalize(db, _appraisal.Id, _companyId, out _);
        winner.AddItem(Guid.NewGuid(), _appraisal.Id, 1, 0m, 3); // unpriced
        await db.SaveChangesAsync(Ct);
        var repository = new QuotationRepository(db);

        Assert.True(await repository.HasFinalizedWonByAsync(_appraisal.Id, _companyId, Ct));
        Assert.False(await repository.HasFinalizedWonByAsync(_appraisal.Id, Guid.NewGuid(), Ct));
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
