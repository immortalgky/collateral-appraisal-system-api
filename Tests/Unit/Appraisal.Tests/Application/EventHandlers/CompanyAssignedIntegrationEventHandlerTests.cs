using Appraisal.Application.EventHandlers;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Quotations;
using Appraisal.Infrastructure;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared.Exceptions;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Time;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Application.EventHandlers;

public class CompanyAssignedIntegrationEventHandlerTests
{
    [Fact]
    public async Task Consume_QuotationMethod_WinnerHasItemOnlyForAnotherAppraisal_Throws_AndCreatesNoFee()
    {
        // Arrange: multi-appraisal RFQ; the winner priced appraisal B only. Its TotalQuotedPrice (> 0) must
        // NOT be used as the fee for appraisal A.
        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "Initial", "Normal", DateTime.Now);
        appraisal.AssignAdmin();
        var companyId = Guid.NewGuid();
        var otherAppraisalId = Guid.NewGuid();

        var rfq = QuotationRequest.Create(DateTime.Now.AddDays(1), "admin", DateTime.Now);
        var winner = CompanyQuotation.Create(rfq.Id, Guid.NewGuid(), companyId, "QT-001", DateTime.Now);
        winner.AddItem(Guid.NewGuid(), otherAppraisalId, 1, 50_000m, 3);
        winner.MarkAsWinner();
        rfq.AddQuotation(winner);
        Assert.True(winner.TotalQuotedPrice > 0m);

        var quotationRepo = Substitute.For<IQuotationRepository>();
        quotationRepo.GetFinalizedByAppraisalIdAsync(appraisal.Id, companyId, Arg.Any<CancellationToken>())
            .Returns(rfq);
        var feeService = Substitute.For<IAssignmentFeeService>();
        var handler = BuildHandler(appraisal, quotationRepo, feeService);

        var ctx = BuildContext(appraisal.Id, companyId);

        // Act + Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => handler.Consume(ctx));
        Assert.Contains("has no price for appraisal", ex.Message);
        await feeService.DidNotReceiveWithAnyArgs()
            .EnsureAssignmentFeeItemsAsync(default, default, default!, default);
    }

    [Fact]
    public async Task Consume_QuotationMethod_RfqWinnerIsAnotherCompany_Throws_AndCreatesNoFee()
    {
        // The repository hands back an RFQ whose winning (IsWinner) quotation belongs to a DIFFERENT company, priced
        // for this appraisal. The event's company must never be billed from someone else's winning quotation.
        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "Initial", "Normal", DateTime.Now);
        appraisal.AssignAdmin();
        var eventCompanyId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();

        var rfq = QuotationRequest.Create(DateTime.Now.AddDays(1), "admin", DateTime.Now);
        var otherWinner = CompanyQuotation.Create(rfq.Id, Guid.NewGuid(), otherCompanyId, "QT-002", DateTime.Now);
        otherWinner.AddItem(Guid.NewGuid(), appraisal.Id, 1, 50_000m, 3);
        otherWinner.MarkAsWinner();
        rfq.AddQuotation(otherWinner);

        var quotationRepo = Substitute.For<IQuotationRepository>();
        quotationRepo.GetFinalizedByAppraisalIdAsync(appraisal.Id, eventCompanyId, Arg.Any<CancellationToken>())
            .Returns(rfq);
        var feeService = Substitute.For<IAssignmentFeeService>();
        var handler = BuildHandler(appraisal, quotationRepo, feeService);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => handler.Consume(BuildContext(appraisal.Id, eventCompanyId)));

        Assert.Contains("has no winning company quotation", ex.Message);
        await feeService.DidNotReceiveWithAnyArgs()
            .EnsureAssignmentFeeItemsAsync(default, default, default!, default);
    }

    [Fact]
    public async Task Consume_QuotationMethod_NoFinalizedRfqWonByCompany_Throws_WithCombinedMessage()
    {
        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "Initial", "Normal", DateTime.Now);
        appraisal.AssignAdmin();
        var companyId = Guid.NewGuid();
        var quotationRepo = Substitute.For<IQuotationRepository>(); // returns null
        var handler = BuildHandler(appraisal, quotationRepo, Substitute.For<IAssignmentFeeService>());

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => handler.Consume(BuildContext(appraisal.Id, companyId)));

        Assert.Contains("was not won by company", ex.Message);
    }

    [Fact]
    public async Task Consume_QuotationMethod_PricedWinnerItem_UsesExVatFeeAfterDiscount_AndBackfillsQuotationRequestId()
    {
        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "Initial", "Normal", DateTime.Now);
        appraisal.AssignAdmin();
        var assignment = appraisal.Assignments.Single();
        Assert.Null(assignment.QuotationRequestId);
        var companyId = Guid.NewGuid();

        var rfq = QuotationRequest.Create(DateTime.Now.AddDays(1), "admin", DateTime.Now);
        rfq.SetQuotationNumber("RFQ-001");
        var winner = CompanyQuotation.Create(rfq.Id, Guid.NewGuid(), companyId, "QT-001", DateTime.Now);
        // QuotedPrice 963 is the VAT-inclusive figure the fee must NOT be taken from.
        var item = winner.AddItem(Guid.NewGuid(), appraisal.Id, 1, 963m, 3);
        item.SetFeeBreakdown(feeAmount: 1000m, discount: 100m, vatPercent: 7m);
        winner.MarkAsWinner();
        rfq.AddQuotation(winner);
        Assert.Equal(900m, item.FeeAfterDiscount);

        var quotationRepo = Substitute.For<IQuotationRepository>();
        quotationRepo.GetFinalizedByAppraisalIdAsync(appraisal.Id, companyId, Arg.Any<CancellationToken>()).Returns(rfq);
        var feeService = Substitute.For<IAssignmentFeeService>();
        feeService.ResolveSourceForAppraisalAsync(
                Arg.Any<AppraisalAggregate>(), Arg.Any<AssignmentFeeSource>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<AssignmentFeeSource>(1)); // not a CI follow-up: source unchanged
        var handler = BuildHandler(appraisal, quotationRepo, feeService);

        await handler.Consume(BuildContext(appraisal.Id, companyId));

        await feeService.Received(1).EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id,
            new AssignmentFeeSource.Quotation(900m, rfq.Id, "RFQ-001"),
            Arg.Any<CancellationToken>());
        Assert.Equal(rfq.Id, assignment.QuotationRequestId);
    }

    [Fact]
    public async Task Consume_QuotationMethod_LegacyItemWithoutBreakdown_UsesQuotedPrice()
    {
        var (appraisal, assignment, companyId, rfq) = QuotedRfq(quotedPrice: 800m);
        var feeService = PassThroughFeeService();
        var handler = BuildHandler(appraisal, RepoReturning(appraisal, companyId, rfq), feeService);

        await handler.Consume(BuildContext(appraisal.Id, companyId));

        await feeService.Received(1).EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id,
            new AssignmentFeeSource.Quotation(800m, rfq.Id, "RFQ-001"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_QuotationMethod_ItemPricedAtZero_Throws_AndCreatesNoFee()
    {
        var (appraisal, _, companyId, rfq) = QuotedRfq(quotedPrice: 0m);
        var feeService = PassThroughFeeService();
        var handler = BuildHandler(appraisal, RepoReturning(appraisal, companyId, rfq), feeService);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => handler.Consume(BuildContext(appraisal.Id, companyId)));

        Assert.Contains("no usable price", ex.Message);
        await feeService.DidNotReceiveWithAnyArgs()
            .EnsureAssignmentFeeItemsAsync(default, default, default!, default);
    }

    // Winner item for this appraisal with only a QuotedPrice (legacy row: FeeAmount 0, no SetFeeBreakdown).
    private static (AppraisalAggregate Appraisal, AppraisalAssignment Assignment, Guid CompanyId, QuotationRequest Rfq)
        QuotedRfq(decimal quotedPrice)
    {
        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "Initial", "Normal", DateTime.Now);
        appraisal.AssignAdmin();
        var companyId = Guid.NewGuid();
        var rfq = QuotationRequest.Create(DateTime.Now.AddDays(1), "admin", DateTime.Now);
        rfq.SetQuotationNumber("RFQ-001");
        var winner = CompanyQuotation.Create(rfq.Id, Guid.NewGuid(), companyId, "QT-001", DateTime.Now);
        winner.AddItem(Guid.NewGuid(), appraisal.Id, 1, quotedPrice, 3);
        winner.MarkAsWinner();
        rfq.AddQuotation(winner);
        return (appraisal, appraisal.Assignments.Single(), companyId, rfq);
    }

    private static IQuotationRepository RepoReturning(AppraisalAggregate appraisal, Guid companyId, QuotationRequest rfq)
    {
        var repo = Substitute.For<IQuotationRepository>();
        repo.GetFinalizedByAppraisalIdAsync(appraisal.Id, companyId, Arg.Any<CancellationToken>()).Returns(rfq);
        return repo;
    }

    private static IAssignmentFeeService PassThroughFeeService()
    {
        var feeService = Substitute.For<IAssignmentFeeService>();
        feeService.ResolveSourceForAppraisalAsync(
                Arg.Any<AppraisalAggregate>(), Arg.Any<AssignmentFeeSource>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<AssignmentFeeSource>(1)); // not a CI follow-up: source unchanged
        return feeService;
    }

    private static ConsumeContext<CompanyAssignedIntegrationEvent> BuildContext(Guid appraisalId, Guid companyId)
    {
        var ctx = Substitute.For<ConsumeContext<CompanyAssignedIntegrationEvent>>();
        ctx.Message.Returns(new CompanyAssignedIntegrationEvent
        {
            AppraisalId = appraisalId,
            CompanyId = companyId,
            CompanyName = "Acme",
            AssignmentMethod = "Quotation"
        });
        ctx.MessageId.Returns((Guid?)null); // null → InboxGuard is a complete no-op (no DB access)
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }

    private static CompanyAssignedIntegrationEventHandler BuildHandler(
        AppraisalAggregate appraisal, IQuotationRepository quotationRepo, IAssignmentFeeService feeService)
    {
        var repo = Substitute.For<IAppraisalRepository>();
        repo.GetByIdWithAllDataAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(appraisal);

        var db = new AppraisalDbContext(new DbContextOptionsBuilder<AppraisalDbContext>().Options);
        var inboxGuard = new InboxGuard<AppraisalDbContext>(
            db,
            NullLogger<InboxGuard<AppraisalDbContext>>.Instance,
            Substitute.For<IDateTimeProvider>());

        return new CompanyAssignedIntegrationEventHandler(
            repo, quotationRepo, Substitute.For<IAppraisalUnitOfWork>(), feeService,
            NullLogger<CompanyAssignedIntegrationEventHandler>.Instance, inboxGuard);
    }
}
