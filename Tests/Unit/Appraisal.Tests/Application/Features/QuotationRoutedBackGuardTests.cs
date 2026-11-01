using Appraisal.Application.Features.Quotations.AddAppraisalToDraft;
using Appraisal.Application.Features.Quotations.CreateQuotation;
using Appraisal.Application.Features.Quotations.Shared;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Quotations;
using Appraisal.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shared.Data;
using Shared.Data.Outbox;
using Shared.Exceptions;
using Shared.Identity;
using Shared.Time;

namespace Appraisal.Tests.Application.Features;

/// <summary>The other two quotation entry points refuse a routed-back (non-Pending) appraisal like StartQuotationFromTask does.</summary>
public class QuotationRoutedBackGuardTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly AppraisalDbContext _db = new(new DbContextOptionsBuilder<AppraisalDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly IQuotationRepository _quotations = Substitute.For<IQuotationRepository>();
    private readonly Guid _appraisalId = Guid.NewGuid();

    private async Task SeedInProgressRow()
    {
        var row = AppraisalAssignment.Create(_appraisalId, "External", Guid.NewGuid().ToString(), "Manual", assignedBy: "system");
        row.Assign("External", assigneeCompanyId: Guid.NewGuid().ToString(), assignmentMethod: "Manual", assignedBy: "system");
        row.StartWork();
        _db.AppraisalAssignments.Add(row);
        await _db.SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task AddAppraisalToDraft_refuses_a_routed_back_appraisal()
    {
        await SeedInProgressRow();
        var draft = QuotationRequest.Create(DateTime.Now.AddDays(1), "admin", DateTime.Now);
        _quotations.GetByIdAsync(draft.Id, Arg.Any<CancellationToken>()).Returns(draft);
        var user = Substitute.For<ICurrentUserService>();
        user.IsInRole("IntAdmin").Returns(true);
        user.Username.Returns("admin");
        var handler = new AddAppraisalToDraftCommandHandler(
            _quotations, Substitute.For<IAppraisalRepository>(), new QuotationAssignmentGuard(_db), user,
            Substitute.For<IIntegrationEventOutbox>(), Substitute.For<IDateTimeProvider>());

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => handler.Handle(new AddAppraisalToDraftCommand(draft.Id, _appraisalId), Ct));

        Assert.Contains("Decision Summary", ex.Message);
    }

    [Fact]
    public async Task CreateQuotation_refuses_a_request_holding_a_routed_back_appraisal()
    {
        await SeedInProgressRow();
        var handler = new CreateQuotationCommandHandler(
            _quotations, Substitute.For<IAppraisalRepository>(), new QuotationAssignmentGuard(_db), Substitute.For<ISqlConnectionFactory>(),
            Substitute.For<ICurrentUserService>(), Substitute.For<IQuotationActivityLogger>(),
            Substitute.For<IDateTimeProvider>());

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new CreateQuotationCommand(DateTime.Now.AddDays(1), "admin",
                Appraisals: [new CreateQuotationAppraisalDto(_appraisalId, null)]), Ct));

        Assert.Contains("Decision Summary", ex.Message);
        await _quotations.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    // No assignment row at all passes the guard; the next dependency (appraisal summary lookup) then finds nothing,
    // so reaching its NotFound / BadRequest proves the guard let it through.
    [Fact]
    public async Task AddAppraisalToDraft_gets_past_the_guard_when_there_is_no_assignment_row()
    {
        var draft = QuotationRequest.Create(DateTime.Now.AddDays(1), "admin", DateTime.Now);
        _quotations.GetByIdAsync(draft.Id, Arg.Any<CancellationToken>()).Returns(draft);
        var user = Substitute.For<ICurrentUserService>();
        user.IsInRole("IntAdmin").Returns(true);
        user.Username.Returns("admin");
        var handler = new AddAppraisalToDraftCommandHandler(
            _quotations, Substitute.For<IAppraisalRepository>(), new QuotationAssignmentGuard(_db), user,
            Substitute.For<IIntegrationEventOutbox>(), Substitute.For<IDateTimeProvider>());

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new AddAppraisalToDraftCommand(draft.Id, _appraisalId), Ct));
    }

    [Fact]
    public async Task CreateQuotation_gets_past_the_guard_when_there_is_no_assignment_row()
    {
        var handler = new CreateQuotationCommandHandler(
            _quotations, Substitute.For<IAppraisalRepository>(), new QuotationAssignmentGuard(_db),
            Substitute.For<ISqlConnectionFactory>(), Substitute.For<ICurrentUserService>(),
            Substitute.For<IQuotationActivityLogger>(), Substitute.For<IDateTimeProvider>());

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new CreateQuotationCommand(DateTime.Now.AddDays(1), "admin",
                Appraisals: [new CreateQuotationAppraisalDto(_appraisalId, null)]), Ct));

        Assert.Contains("not found", ex.Message);
    }

    private async Task Seed(Guid appraisalId, bool inProgress, DateTime createdAt)
    {
        var row = AppraisalAssignment.Create(appraisalId, "External", Guid.NewGuid().ToString(), "Manual", assignedBy: "system");
        if (inProgress)
        {
            row.Assign("External", assigneeCompanyId: Guid.NewGuid().ToString(), assignmentMethod: "Manual", assignedBy: "system");
            row.StartWork();
        }
        row.CreatedAt = createdAt;
        _db.AppraisalAssignments.Add(row);
        await _db.SaveChangesAsync(Ct);
    }

    // Whichever of A (Pending) and B (InProgress) holds the newest row overall, only B is refused, and nothing is created.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateQuotation_with_two_appraisals_names_the_routed_back_one(bool bRowIsNewest)
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await Seed(a, false, new DateTime(2026, 1, bRowIsNewest ? 1 : 2));
        await Seed(b, true, new DateTime(2026, 1, bRowIsNewest ? 2 : 1));
        var handler = new CreateQuotationCommandHandler(
            _quotations, Substitute.For<IAppraisalRepository>(), new QuotationAssignmentGuard(_db),
            Substitute.For<ISqlConnectionFactory>(), Substitute.For<ICurrentUserService>(),
            Substitute.For<IQuotationActivityLogger>(), Substitute.For<IDateTimeProvider>());

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new CreateQuotationCommand(DateTime.Now.AddDays(1), "admin",
                Appraisals: [new CreateQuotationAppraisalDto(a, null), new CreateQuotationAppraisalDto(b, null)]), Ct));

        Assert.Contains(b.ToString(), ex.Message);
        Assert.DoesNotContain(a.ToString(), ex.Message);
        await _quotations.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }
}
