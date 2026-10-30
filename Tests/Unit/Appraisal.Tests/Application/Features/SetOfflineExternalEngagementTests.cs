using Appraisal.Application.Features.Assignments.SetOfflineExternalEngagement;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Auth.Contracts.Companies;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared.Exceptions;
using Shared.Identity;
using static Appraisal.Tests.Application.Services.FeeTestSeed;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Application.Features;

public class SetOfflineExternalEngagementTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly AppraisalDbContext _db = NewDb();
    private readonly IAppraisalRepository _appraisals = Substitute.For<IAppraisalRepository>();
    private readonly ICompanyLookupService _companies = Substitute.For<ICompanyLookupService>();
    private readonly IAssignmentFeeService _fees = Substitute.For<IAssignmentFeeService>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly Guid _companyId = Guid.NewGuid();

    public SetOfflineExternalEngagementTests()
    {
        _companies.GetByIdAsync(_companyId, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(_companyId, "Acme", null, IsAssignable: true));
        _fees.ResolveSourceForAppraisalAsync(
                Arg.Any<AppraisalAggregate>(), Arg.Any<AssignmentFeeSource>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<AssignmentFeeSource>(1));
    }

    // The row as the InternalAssigned consumer leaves it on int-offline-book-keyin (Offline, InProgress), with
    // whatever assigner it carried.
    private (AppraisalAggregate Appraisal, AppraisalAssignment Assignment) Seed(string assignedBy)
    {
        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
        var assignment = appraisal.AssignAdmin();
        assignment.Assign("External", assigneeUserId: "keyer", assignmentMethod: AppraisalAssignment.OfflineAssignmentMethod,
            assignedBy: assignedBy);
        assignment.StartWork();
        _appraisals.GetByIdWithAllDataAsync(appraisal.Id, Arg.Any<CancellationToken>()).Returns(appraisal);
        return (appraisal, assignment);
    }

    private Task Handle(Guid appraisalId) => Handle(appraisalId, _companyId);

    private Task Handle(Guid appraisalId, Guid? companyId, string? appraiserName = null) =>
        new SetOfflineExternalEngagementCommandHandler(
                _appraisals, _companies, _fees, _db, _user, NullLogger<SetOfflineExternalEngagementCommandHandler>.Instance)
            .Handle(new SetOfflineExternalEngagementCommand(appraisalId, companyId, new DateTime(2026, 1, 5), appraiserName), Ct);

    // EXTO assigned under #618: the admin recorded the company at assignment, so it is on the row when the keyer saves.
    private (AppraisalAggregate Appraisal, AppraisalAssignment Assignment) SeedWithAdminsCompany(Guid companyId)
    {
        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
        var assignment = appraisal.AssignAdmin();
        assignment.SetAssigner("P5229");
        assignment.SetAssigneeCompany(companyId.ToString());
        assignment.Assign("External", assigneeUserId: "keyer", assigneeCompanyId: companyId.ToString(),
            assignmentMethod: AppraisalAssignment.OfflineAssignmentMethod, assignedBy: "P5229");
        assignment.StartWork();
        _appraisals.GetByIdWithAllDataAsync(appraisal.Id, Arg.Any<CancellationToken>()).Returns(appraisal);
        return (appraisal, assignment);
    }

    [Theory]
    [InlineData(false)] // the keyer's page shows the company read-only and sends none
    [InlineData(true)]  // an older page sends the company it was shown
    public async Task With_the_admins_company_on_the_row_the_book_date_and_appraiser_are_recorded_and_the_company_kept(
        bool sendSameCompany)
    {
        var adminsCompany = Guid.NewGuid();
        var (appraisal, assignment) = SeedWithAdminsCompany(adminsCompany);

        await Handle(appraisal.Id, sendSameCompany ? adminsCompany : null, "Somchai");

        Assert.Equal(adminsCompany.ToString(), assignment.AssigneeCompanyId);
        Assert.Equal("Somchai", assignment.ExternalAppraiserName);
        Assert.Equal(new DateTime(2026, 1, 5), _db.ValuationAnalyses.Local.Single(v => v.AppraisalId == appraisal.Id).ValuationDate);
        Assert.Equal(AssignmentStatus.InProgress, assignment.AssignmentStatus);
        // Validated at assignment: an MOU that lapsed since must not strand the keyer.
        await _companies.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }

    [Fact]
    public async Task With_the_admins_company_on_the_row_a_different_company_is_refused_and_nothing_is_written()
    {
        var adminsCompany = Guid.NewGuid();
        var (appraisal, assignment) = SeedWithAdminsCompany(adminsCompany);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => Handle(appraisal.Id, _companyId));

        Assert.Contains("route the case back to appraisal assignment", ex.Message);
        Assert.Equal(adminsCompany.ToString(), assignment.AssigneeCompanyId);
        Assert.Empty(_db.ValuationAnalyses.Local);
        await _fees.DidNotReceiveWithAnyArgs().EnsureAssignmentFeeItemsAsync(default, default, default!, default, default);
    }

    // A never-assigned row (AssignedAt null) carries the company of an unvalidated draft, or the admin's EXTO pick in
    // the instant before the InternalAssigned consumer ran. Either way it stays locked, but is checked again.
    private (AppraisalAggregate Appraisal, AppraisalAssignment Assignment) SeedNeverAssignedWithCompany(Guid companyId)
    {
        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
        var assignment = appraisal.AssignAdmin();
        assignment.SaveDraft("External", "keyer-1", companyId.ToString(), AppraisalAssignment.OfflineAssignmentMethod,
            null, null, null);
        _appraisals.GetByIdWithAllDataAsync(appraisal.Id, Arg.Any<CancellationToken>()).Returns(appraisal);
        return (appraisal, assignment);
    }

    [Fact]
    public async Task A_never_assigned_rows_company_is_kept_after_the_existence_and_MOU_check()
    {
        var (appraisal, assignment) = SeedNeverAssignedWithCompany(_companyId);

        await Handle(appraisal.Id, null);

        Assert.Equal(_companyId.ToString(), assignment.AssigneeCompanyId);
        await _companies.Received(1).GetByIdAsync(_companyId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_never_assigned_rows_company_outside_its_MOU_window_is_refused()
    {
        var draftCompany = Guid.NewGuid();
        _companies.GetByIdAsync(draftCompany, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(draftCompany, "Lapsed", null, IsAssignable: false));
        var (appraisal, assignment) = SeedNeverAssignedWithCompany(draftCompany);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => Handle(appraisal.Id, null));

        Assert.Contains("outside its MOU approval window", ex.Message);
        Assert.Equal(AssignmentStatus.Pending, assignment.AssignmentStatus);
        await _fees.DidNotReceiveWithAnyArgs().EnsureAssignmentFeeItemsAsync(default, default, default!, default, default);
    }

    [Fact]
    public async Task A_never_assigned_rows_company_is_locked_too()
    {
        var (appraisal, assignment) = SeedNeverAssignedWithCompany(Guid.NewGuid());

        await Assert.ThrowsAsync<BadRequestException>(() => Handle(appraisal.Id, _companyId));

        Assert.NotEqual(_companyId.ToString(), assignment.AssigneeCompanyId);
    }

    [Fact]
    public async Task Without_a_company_on_the_row_the_keyer_must_still_name_one()
    {
        // A case already at key-in when #618 shipped: nobody recorded a company at assignment.
        var (appraisal, assignment) = Seed("P5229");

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => Handle(appraisal.Id, null));

        Assert.Equal("The external company is required.", ex.Message);
        Assert.Null(assignment.AssigneeCompanyId);
    }

    [Theory]
    [InlineData("System")]
    [InlineData("system")]
    [InlineData("")]
    public async Task A_row_nobody_stamped_takes_the_caller_as_assigner(string stored)
    {
        _user.UserCode.Returns("P0042");
        var (appraisal, assignment) = Seed(stored);

        await Handle(appraisal.Id);

        Assert.Equal("P0042", assignment.AssignedBy);
    }

    [Fact]
    public async Task A_row_nobody_stamped_and_no_caller_stays_System()
    {
        _user.UserCode.Returns((string?)null);
        var (appraisal, assignment) = Seed("System");

        await Handle(appraisal.Id);

        Assert.Equal("System", assignment.AssignedBy);
    }

    [Fact]
    public async Task The_admin_recorded_by_AssignAppraisal_is_kept()
    {
        _user.UserCode.Returns("P0042");
        var (appraisal, assignment) = Seed("P5229");

        await Handle(appraisal.Id);

        Assert.Equal("P5229", assignment.AssignedBy);
    }

    [Fact]
    public async Task A_routed_back_Pending_row_is_refused_and_nothing_is_written_or_priced()
    {
        // ReturnToAssignmentQueue keeps AssignmentMethod "Offline" and AssignedAt, but the workflow is back at
        // appraisal-assignment: there is no keyer, and Assign() would hand the case on behind the admin.
        var (appraisal, assignment) = Seed("P5229");
        assignment.ReturnToAssignmentQueue();
        Assert.Equal(AssignmentStatus.Pending, assignment.AssignmentStatus);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => Handle(appraisal.Id));

        Assert.Contains("awaiting the admin's decision", ex.Message);
        Assert.Equal(AssignmentStatus.Pending, assignment.AssignmentStatus);
        Assert.Null(assignment.AssigneeCompanyId);
        await _fees.DidNotReceiveWithAnyArgs().EnsureAssignmentFeeItemsAsync(default, default, default!, default, default);
    }

    [Fact]
    public async Task A_never_assigned_Pending_Offline_row_is_still_recorded_and_repriced()
    {
        // An admin's EXTO draft (or the instant before the InternalAssigned consumer ran): Pending, AssignedAt null.
        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
        var assignment = appraisal.AssignAdmin();
        assignment.SaveDraft("Internal", "keyer-1", null, AppraisalAssignment.OfflineAssignmentMethod, null, null, null);
        assignment.SetAssigner("P5229");
        _appraisals.GetByIdWithAllDataAsync(appraisal.Id, Arg.Any<CancellationToken>()).Returns(appraisal);
        Assert.Null(assignment.AssignedAt);

        await Handle(appraisal.Id);

        Assert.Equal(_companyId.ToString(), assignment.AssigneeCompanyId);
        Assert.Equal(AssignmentStatus.InProgress, assignment.AssignmentStatus);
        await _fees.Received(1).EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, Arg.Any<AssignmentFeeSource>(), true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_row_on_the_keyin_task_is_recorded_and_priced_without_repricing_a_base()
    {
        _user.UserCode.Returns("P0042");
        var (appraisal, assignment) = Seed("P5229");

        await Handle(appraisal.Id);

        Assert.Equal(_companyId.ToString(), assignment.AssigneeCompanyId);
        Assert.Equal(AssignmentStatus.InProgress, assignment.AssignmentStatus);
        await _fees.Received(1).EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, Arg.Any<AssignmentFeeSource>(), false, Arg.Any<CancellationToken>());
    }
}
