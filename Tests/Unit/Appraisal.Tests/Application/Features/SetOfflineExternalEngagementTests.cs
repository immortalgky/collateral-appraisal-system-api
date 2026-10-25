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

    private Task Handle(Guid appraisalId) =>
        new SetOfflineExternalEngagementCommandHandler(
                _appraisals, _companies, _fees, _db, _user, NullLogger<SetOfflineExternalEngagementCommandHandler>.Instance)
            .Handle(new SetOfflineExternalEngagementCommand(appraisalId, _companyId, new DateTime(2026, 1, 5)), Ct);

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
