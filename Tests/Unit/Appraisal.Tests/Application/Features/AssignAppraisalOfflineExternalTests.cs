using Appraisal.Application.Features.Assignments.AssignAppraisal;
using Appraisal.Application.Features.Assignments.GetAssignments;
using Appraisal.Application.Features.Assignments.SaveAssignmentDraft;
using Appraisal.Application.Features.Fees.GetFeePreview;
using Appraisal.Application.Features.Quotations.AutoAssign;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Quotations;
using Appraisal.Infrastructure;
using Auth.Contracts.Companies;
using Auth.Contracts.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared.Exceptions;
using Shared.Identity;
using Workflow;
using static Appraisal.Tests.Application.Services.FeeTestSeed;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Application.Features;

/// <summary>
/// EXTO (company engaged outside the system): the admin names the company at assignment, under the same
/// existence + MOU-window rule as a manual in-system external pick, and it is recorded on the row before the
/// workflow resumes.
/// </summary>
public class AssignAppraisalOfflineExternalTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IAppraisalRepository _appraisals = Substitute.For<IAppraisalRepository>();
    private readonly IQuotationAutoAssignStateReader _ownership = Substitute.For<IQuotationAutoAssignStateReader>();
    private readonly IWorkflowRelayService _relay = Substitute.For<IWorkflowRelayService>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly ICompanyLookupService _companies = Substitute.For<ICompanyLookupService>();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly AppraisalAggregate _appraisal;

    public AssignAppraisalOfflineExternalTests()
    {
        _appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
        _appraisal.AssignAdmin();
        _appraisals.GetByIdWithAllDataAsync(_appraisal.Id, Arg.Any<CancellationToken>()).Returns(_appraisal);
        _user.UserCode.Returns("P5229");
        _ownership.WorkflowInstanceBelongsToAppraisalAsync(default, default, default).ReturnsForAnyArgs(true);
        _sender.Send(Arg.Any<GetFeePreviewQuery>(), Arg.Any<CancellationToken>())
            .Returns(AssignAppraisalQuotationGuardTests.FeePreviewWithBase(2_500m));
        _companies.GetByIdAsync(_companyId, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(_companyId, "Acme", null, IsAssignable: true));
    }

    private AssignAppraisalCommand Command(string decision, string? company) =>
        new(_appraisal.Id, Guid.NewGuid(), AssigneeCompanyId: company, DecisionTaken: decision);

    private Task Assign(string decision, string? company) =>
        new AssignAppraisalCommandHandler(
                _appraisals, Substitute.For<IAppraisalUnitOfWork>(), _relay, Substitute.For<IQuotationRepository>(),
                _ownership, _sender, _user, _companies)
            .Handle(Command(decision, company), Ct);

    private Task ResumedNever() =>
        _relay.DidNotReceiveWithAnyArgs().ResumeWorkflowAsync(default, default!, default!, default!, default, default);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void EXTO_without_a_company_is_a_validation_error(string? company)
    {
        var result = new AssignAppraisalCommandValidator().Validate(Command("EXTO", company));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AssignAppraisalCommand.AssigneeCompanyId));
    }

    [Theory]
    [InlineData("INT")]
    [InlineData("EXT")]
    [InlineData("C")]
    public void Other_decisions_do_not_require_a_company(string decision)
    {
        Assert.True(new AssignAppraisalCommandValidator().Validate(Command(decision, null)).IsValid);
    }

    [Fact]
    public async Task EXTO_records_the_company_on_the_row_before_the_workflow_resumes()
    {
        string? companyWhenResumed = null;
        _relay.ResumeWorkflowAsync(default, default!, default!, default!, default, default)
            .ReturnsForAnyArgs(_ =>
            {
                companyWhenResumed = _appraisal.Assignments.Single().AssigneeCompanyId;
                return Task.CompletedTask;
            });

        await Assign("EXTO", _companyId.ToString());

        Assert.Equal(_companyId.ToString(), companyWhenResumed);
    }

    [Fact]
    public async Task EXTO_with_a_company_outside_its_MOU_window_is_refused_with_the_manual_paths_message()
    {
        _companies.GetByIdAsync(_companyId, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(_companyId, "Acme", null, IsAssignable: false));

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => Assign("EXTO", _companyId.ToString()));

        // CompanySelectionActivity's no-match reason for a manual pick outside the window.
        Assert.Equal("Selected company is not currently assignable (outside its MOU approval window).", ex.Message);
        Assert.Null(_appraisal.Assignments.Single().AssigneeCompanyId);
        await ResumedNever();
    }

    [Fact]
    public async Task EXTO_with_an_unknown_company_is_refused_and_nothing_resumes()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Assign("EXTO", Guid.NewGuid().ToString()));

        Assert.Null(_appraisal.Assignments.Single().AssigneeCompanyId);
        await ResumedNever();
    }

    [Theory]
    [InlineData("EXT")]
    [InlineData("INT")]
    public async Task Other_decisions_do_not_look_the_company_up(string decision)
    {
        await Assign(decision, _companyId.ToString());

        await _companies.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }

    [Fact]
    public async Task An_EXTO_draft_keeps_its_company_and_returns_it_on_reload()
    {
        var db = NewDb();
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        await AddAssignmentAsync(db, appraisal.Id);
        var repo = Substitute.For<IAppraisalRepository>();
        repo.GetByIdWithAllDataAsync(appraisal.Id, Arg.Any<CancellationToken>())
            .Returns(_ => db.Appraisals.Include(a => a.Assignments).First(a => a.Id == appraisal.Id));

        // The admin page stores EXTO as External + method "Offline" (AdministrationPage OFFLINE_DRAFT_METHOD).
        await new SaveAssignmentDraftCommandHandler(repo).Handle(
            new SaveAssignmentDraftCommand(appraisal.Id, "External", AssigneeUserId: "keyer",
                AssigneeCompanyId: _companyId.ToString(), AssignmentMethod: AppraisalAssignment.OfflineAssignmentMethod),
            Ct);
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();

        var reloaded = await new GetAssignmentsQueryHandler(
                db, Substitute.For<IUserLookupService>(), NullLogger<GetAssignmentsQueryHandler>.Instance)
            .Handle(new GetAssignmentsQuery(appraisal.Id), Ct);

        var draft = Assert.Single(reloaded.Assignments);
        Assert.Equal(_companyId.ToString(), draft.AssigneeCompanyId);
        Assert.Equal(AppraisalAssignment.OfflineAssignmentMethod, draft.AssignmentMethod);
    }
}
