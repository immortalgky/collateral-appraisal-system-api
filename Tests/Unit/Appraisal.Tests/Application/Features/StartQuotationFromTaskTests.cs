using Appraisal.Application.Features.Quotations.Shared;
using Appraisal.Application.Features.Quotations.StartQuotationFromTask;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Quotations;
using Appraisal.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared.Data;
using Shared.Data.Outbox;
using Shared.Exceptions;
using Shared.Identity;
using Shared.Time;

namespace Appraisal.Tests.Application.Features;

public class StartQuotationFromTaskTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly AppraisalDbContext _db = new(new DbContextOptionsBuilder<AppraisalDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly IQuotationRepository _quotations = Substitute.For<IQuotationRepository>();
    private readonly IAppraisalRepository _appraisals = Substitute.For<IAppraisalRepository>();

    private StartQuotationFromTaskCommandHandler Handler()
    {
        var user = Substitute.For<ICurrentUserService>();
        user.IsInRole("IntAdmin").Returns(true);
        user.Username.Returns("admin");
        return new StartQuotationFromTaskCommandHandler(
            _quotations, new QuotationAssignmentGuard(_db), _appraisals, user, Substitute.For<ISqlConnectionFactory>(),
            Substitute.For<IIntegrationEventOutbox>(), Substitute.For<IQuotationActivityLogger>(),
            Substitute.For<IDateTimeProvider>(), NullLogger<StartQuotationFromTaskCommandHandler>.Instance);
    }

    private Task<StartQuotationFromTaskResult> Start(Guid appraisalId) => Handler().Handle(
        new StartQuotationFromTaskCommand(appraisalId, Guid.NewGuid(), Guid.NewGuid(), null,
            DateTime.Now.AddDays(1), [Guid.NewGuid()]), Ct);

    // Seeds one live row; createdAt orders it against other rows of the same appraisal.
    private async Task Seed(Guid appraisalId, string status, DateTime createdAt)
    {
        var row = AppraisalAssignment.Create(appraisalId, "External", Guid.NewGuid().ToString(), "Manual", assignedBy: "system");
        if (status == "Rejected") row.Reject("test");
        else if (status == "Cancelled") row.Cancel("test");
        else if (status != "Pending")
        {
            row.Assign("External", assigneeCompanyId: Guid.NewGuid().ToString(), assignmentMethod: "Manual", assignedBy: "system");
            if (status == "InProgress") row.StartWork();
        }
        row.CreatedAt = createdAt;
        _db.AppraisalAssignments.Add(row);
        await _db.SaveChangesAsync(Ct);
    }

    [Theory]
    [InlineData("Assigned")]
    [InlineData("InProgress")]
    public async Task A_current_row_that_is_not_Pending_is_refused_before_anything_is_created(string status)
    {
        var id = Guid.NewGuid();
        await Seed(id, status, new DateTime(2026, 1, 1));

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => Start(id));

        Assert.Contains("Decision Summary", ex.Message);
        Assert.Contains(status, ex.Message);
        await _quotations.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task An_older_Pending_row_beside_a_newer_InProgress_one_is_refused()
    {
        var id = Guid.NewGuid();
        await Seed(id, "Pending", new DateTime(2026, 1, 1));
        await Seed(id, "InProgress", new DateTime(2026, 2, 1));

        await Assert.ThrowsAsync<BadRequestException>(() => Start(id));
    }

    [Fact]
    public async Task An_older_non_Pending_row_beside_a_newer_Pending_current_one_proceeds()
    {
        var id = Guid.NewGuid();
        await Seed(id, "InProgress", new DateTime(2026, 1, 1));
        await Seed(id, "Pending", new DateTime(2026, 2, 1));
        _appraisals.GetSummariesAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([new AppraisalSummary(id, "A-1", "L", null, null, null)]);

        await Start(id);

        await _quotations.ReceivedWithAnyArgs(1).AddAsync(default!, default);
    }

    // The global query filter drops Rejected / Cancelled rows, so a newer one must not hide the live Pending row.
    [Theory]
    [InlineData("Rejected")]
    [InlineData("Cancelled")]
    public async Task A_newer_Rejected_or_Cancelled_row_does_not_hide_the_older_Pending_one(string newerStatus)
    {
        var id = Guid.NewGuid();
        await Seed(id, "Pending", new DateTime(2026, 1, 1));
        await Seed(id, newerStatus, new DateTime(2026, 2, 1));
        _appraisals.GetSummariesAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([new AppraisalSummary(id, "A-1", "L", null, null, null)]);

        await Start(id);

        await _quotations.ReceivedWithAnyArgs(1).AddAsync(default!, default);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_Pending_current_row_or_no_row_at_all_proceeds(bool hasRow)
    {
        var id = Guid.NewGuid();
        if (hasRow) await Seed(id, "Pending", new DateTime(2026, 1, 1));
        _appraisals.GetSummariesAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([new AppraisalSummary(id, "A-1", "L", null, null, null)]);

        await Start(id);

        await _quotations.ReceivedWithAnyArgs(1).AddAsync(default!, default);
    }
}
