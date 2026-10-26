using Appraisal.Application.Features.Assignments.GetAssignmentDefaults;
using Appraisal.Application.Features.Fees.AddFeeItem;
using Appraisal.Contracts.Appraisals;
using Appraisal.Application.Features.Fees.GetFeePreview;
using Appraisal.Application.Features.Fees.RemoveFeeItem;
using Appraisal.Application.Features.Fees.UpdateFee;
using Appraisal.Application.Features.Quotations.AutoAssign;
using Appraisal.Application.Features.Fees.UpdateFeeItem;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Quotations;
using Appraisal.Infrastructure;
using Auth.Domain.Companies;
using Auth.Infrastructure;
using Integration.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Exceptions;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// The fee preview, the admin's pre-assignment fee editing and the previousCompany of assignment-defaults against a
/// real SQL Server: the EF projections (nested items, value-converted assignment status, the latest-fee join) only
/// have to translate there, the unit tests run them on the in-memory provider.
/// </summary>
[Collection("Integration")]
public class FeePreviewAndAdminFeeTests(IntegrationTestFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IServiceScope CreateScope() => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        using var scope = CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request, Ct);
    }

    private async Task<(Guid AppraisalId, Guid AssignmentId)> SeedAsync(
        string type = "New", Guid? prevId = null, bool assigned = false, string? companyId = null, string? number = null)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), type, "Normal", DateTime.Now, prevAppraisalId: prevId);
        appraisal.SetAppraisalNumber(number ?? $"AP-{Guid.NewGuid():N}"[..14]);
        db.Appraisals.Add(appraisal);
        await db.SaveChangesAsync(Ct);

        var assignment = AppraisalAssignment.Create(
            appraisal.Id, companyId is null ? "Internal" : "External", assigneeCompanyId: companyId, assignedBy: "test");
        if (assigned)
            assignment.Assign(companyId is null ? "Internal" : "External", assigneeUserId: companyId is null ? "U1" : null,
                assigneeCompanyId: companyId, assignedBy: "test");
        db.AppraisalAssignments.Add(assignment);
        await db.SaveChangesAsync(Ct);
        return (appraisal.Id, assignment.Id);
    }

    private async Task<Guid> AddFeeAsync(Guid assignmentId, decimal? sellingPrice = null, decimal? inspectionFee = null)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var fee = AppraisalFee.Create(assignmentId, totalSellingPrice: sellingPrice);
        fee.SetConstructionInspectionFee(inspectionFee);
        db.AppraisalFees.Add(fee);
        await db.SaveChangesAsync(Ct);
        return fee.Id;
    }

    private async Task<List<AppraisalFeeItem>> ItemsOfAsync(Guid appraisalId)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        return await db.AppraisalFees
            .Where(f => db.AppraisalAssignments.Any(a => a.AppraisalId == appraisalId && a.Id == f.AssignmentId))
            .SelectMany(f => f.Items)
            .ToListAsync(Ct);
    }

    [Fact]
    public async Task Preview_Tier_TranslatesAndShowsTheBand()
    {
        var (appraisalId, assignmentId) = await SeedAsync();
        await AddFeeAsync(assignmentId, sellingPrice: 8_000_000m);

        var preview = await SendAsync(new GetFeePreviewQuery(appraisalId, null, null, null));

        Assert.Equal(3_000m, preview.SystemBaseAmount);
        Assert.Equal(FeeBaseKinds.Tier, preview.BaseSource.Kind);
        Assert.Equal(8_000_000m, preview.BaseSource.SellingPrice);
        Assert.Equal(3_210m, preview.Total);
    }

    [Fact]
    public async Task Preview_Progressive_ReadsThePickedPriorsKeyedInspectionFee()
    {
        var (priorId, priorAssignment) = await SeedAsync(assigned: true, number: "AP-PRIOR-IT");
        await AddFeeAsync(priorAssignment, inspectionFee: 3_000m);
        var (appraisalId, assignmentId) = await SeedAsync("Progressive", prevId: priorId);
        await AddFeeAsync(assignmentId, sellingPrice: 1m);

        var preview = await SendAsync(new GetFeePreviewQuery(appraisalId, null, null, null));

        Assert.Equal(FeeBaseKinds.ConstructionInspectionFee, preview.BaseSource.Kind);
        Assert.Equal("AP-PRIOR-IT", preview.BaseSource.AppraisalNumber);
        Assert.Equal(3_000m, preview.SystemBaseAmount);
    }

    [Fact]
    public async Task Preview_Quotation_TranslatesTheWinnersProjection_AndOnlyShowsTheWinnersPrice()
    {
        var (appraisalId, assignmentId) = await SeedAsync();
        await AddFeeAsync(assignmentId, sellingPrice: 1_000_000m);
        var winner = Guid.NewGuid();
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var now = DateTime.Now;
            var rfq = QuotationRequest.Create(now.AddDays(1), "admin", now);
            rfq.SetQuotationNumber($"RFQ-{Guid.NewGuid():N}"[..14]);
            rfq.AddAppraisal(appraisalId, "admin", now);
            var won = CompanyQuotation.Create(rfq.Id, Guid.NewGuid(), winner, "QT-IT", now);
            won.AddItem(Guid.NewGuid(), appraisalId, 1, 4_815m, 3).SetFeeBreakdown(4_500m, 300m, vatPercent: 7m);
            won.MarkAsWinner();
            rfq.AddQuotation(won);
            typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.Status))!.SetValue(rfq, "Finalized");
            typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.SelectedCompanyId))!.SetValue(rfq, winner);
            typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.SelectedAt))!.SetValue(rfq, now);
            db.QuotationRequests.Add(rfq);
            await db.SaveChangesAsync(Ct);
        }

        var forWinner = await SendAsync(new GetFeePreviewQuery(appraisalId, "external", "quotation", winner));
        var forAnother = await SendAsync(new GetFeePreviewQuery(appraisalId, "external", "quotation", Guid.NewGuid()));

        Assert.Equal(FeeBaseKinds.Quotation, forWinner.BaseSource.Kind);
        Assert.Equal(4_200m, forWinner.SystemBaseAmount); // fee after discount, ex-VAT
        Assert.Equal(FeeBaseKinds.QuotationPending, forAnother.BaseSource.Kind);
        Assert.Null(forAnother.SystemBaseAmount);
    }

    private async Task<(Guid RfqAppraisalId, Guid Winner)> SeedFinalizedQuotationAsync(decimal feeAmount, decimal quotedPrice)
    {
        var (appraisalId, _) = await SeedAsync();
        var winner = Guid.NewGuid();
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var now = DateTime.Now;
        var rfq = QuotationRequest.Create(now.AddDays(1), "admin", now);
        rfq.SetQuotationNumber($"RFQ-{Guid.NewGuid():N}"[..14]);
        rfq.AddAppraisal(appraisalId, "admin", now);
        var won = CompanyQuotation.Create(rfq.Id, Guid.NewGuid(), winner, "QT-IT", now);
        var item = won.AddItem(Guid.NewGuid(), appraisalId, 1, quotedPrice, 3);
        if (feeAmount > 0m)
            item.SetFeeBreakdown(feeAmount, 0m, vatPercent: 7m);
        won.MarkAsWinner();
        rfq.AddQuotation(won);
        typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.Status))!.SetValue(rfq, "Finalized");
        typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.SelectedCompanyId))!.SetValue(rfq, winner);
        typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.SelectedAt))!.SetValue(rfq, now);
        db.QuotationRequests.Add(rfq);
        await db.SaveChangesAsync(Ct);
        return (appraisalId, winner);
    }

    [Fact]
    public async Task QuotationGuard_OnlyAsksWhoWon_TranslatesToSql_AndIgnoresThePrice()
    {
        var (pricedId, pricedWinner) = await SeedFinalizedQuotationAsync(feeAmount: 4_500m, quotedPrice: 4_815m);
        var (unpricedId, unpricedWinner) = await SeedFinalizedQuotationAsync(feeAmount: 0m, quotedPrice: 0m);

        using var scope = CreateScope();
        var quotations = scope.ServiceProvider.GetRequiredService<IQuotationRepository>();

        Assert.True(await quotations.HasFinalizedWonByAsync(pricedId, pricedWinner, Ct));
        Assert.True(await quotations.HasFinalizedWonByAsync(unpricedId, unpricedWinner, Ct)); // price is the 01 rule's business
        Assert.False(await quotations.HasFinalizedWonByAsync(unpricedId, Guid.NewGuid(), Ct)); // not the winner
    }

    [Fact]
    public async Task AutoAssignState_ReportsAnAdminBaseOnTheCurrentAssignment()
    {
        var (withAdmin, assignmentA) = await SeedAsync();
        var (without, assignmentB) = await SeedAsync();
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var feeA = AppraisalFee.Create(assignmentA);
            feeA.AddItem("01", "admin base", 3_500m).MarkAsAdminSet();
            var feeB = AppraisalFee.Create(assignmentB);
            feeB.AddItem("01", "system base", 2_500m);
            db.AppraisalFees.AddRange(feeA, feeB);
            await db.SaveChangesAsync(Ct);
        }

        using var readScope = CreateScope();
        var states = await readScope.ServiceProvider.GetRequiredService<IQuotationAutoAssignStateReader>()
            .GetAsync([withAdmin, without], Ct);

        Assert.True(states.Single(x => x.AppraisalId == withAdmin).HasAdminBaseFee);
        Assert.False(states.Single(x => x.AppraisalId == without).HasAdminBaseFee);
    }

    [Fact]
    public async Task HeaderSave_AtTheAdminStage_ToleratesAnAbsorbAbovePartialTotal_AndAnAllZeroFeeIdIs404()
    {
        var (appraisalId, assignmentId) = await SeedAsync();
        var feeId = await AddFeeAsync(assignmentId, sellingPrice: 1_000_000m);
        await SendAsync(new AddFeeItemCommand(appraisalId, feeId, "02", "Travel", 500m));

        await SendAsync(new UpdateFeeCommand(feeId, "02", 3_000m));
        await SendAsync(new UpdateFeeCommand(feeId, "02", 3_000m)); // the page re-sends the stored figure

        using (var scope = CreateScope())
        {
            var fee = await scope.ServiceProvider.GetRequiredService<AppraisalDbContext>()
                .AppraisalFees.AsNoTracking().SingleAsync(f => f.Id == feeId, Ct);
            Assert.Equal(3_000m, fee.BankAbsorbAmount);
        }

        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(new AddFeeItemCommand(appraisalId, Guid.Empty, "02", "Travel", 500m)));
    }

    [Fact]
    public async Task Preview_Quotation_WithTwoItems_ShowsTheItemTheConsumerWouldBill()
    {
        var (appraisalId, winner) = await SeedFinalizedQuotationAsync(feeAmount: 4_500m, quotedPrice: 4_815m);
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var rfq = await db.QuotationRequests.Include(q => q.Quotations).ThenInclude(c => c.Items).SingleAsync(
                q => q.Appraisals.Any(a => a.AppraisalId == appraisalId), Ct);
            // A second, dearer item for the same appraisal with a LATER item number.
            rfq.Quotations.Single().AddItem(Guid.NewGuid(), appraisalId, 9, 99m, 3)
                .SetFeeBreakdown(7_000m, 0m, vatPercent: 7m);
            await db.SaveChangesAsync(Ct);
        }

        using var read = CreateScope();
        var rfqRead = await read.ServiceProvider.GetRequiredService<AppraisalDbContext>().QuotationRequests
            .Include(q => q.Quotations).ThenInclude(c => c.Items)
            .SingleAsync(q => q.Appraisals.Any(a => a.AppraisalId == appraisalId), Ct);
        var consumer = QuotationFeePrice.Resolve(rfqRead, appraisalId, winner);

        // The fee row the preview needs.
        var assignment = await read.ServiceProvider.GetRequiredService<AppraisalDbContext>().AppraisalAssignments
            .SingleAsync(a => a.AppraisalId == appraisalId, Ct);
        await AddFeeAsync(assignment.Id, sellingPrice: 1_000_000m);
        var preview = await SendAsync(new GetFeePreviewQuery(appraisalId, "external", "quotation", winner));

        Assert.Equal(4_500m, consumer.Amount);
        Assert.Equal(consumer.Amount, preview.SystemBaseAmount);
    }

    [Fact]
    public async Task UpdateFeeItem_OfALegacyCodeOutsideTypeOfFee_IsAccepted_UntilTheCodeChanges()
    {
        var (appraisalId, assignmentId) = await SeedAsync();
        Guid feeId, itemId;
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var fee = AppraisalFee.Create(assignmentId, feePaymentType: "01", totalSellingPrice: 1_000_000m);
            var item = fee.AddItem("03", "legacy urgent", 100m);
            item.MarkAsAdminSet();
            db.AppraisalFees.Add(fee);
            await db.SaveChangesAsync(Ct);
            (feeId, itemId) = (fee.Id, item.Id);
        }

        await SendAsync(new UpdateFeeItemCommand(appraisalId, feeId, itemId, "03", "legacy urgent", 150m));
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            SendAsync(new UpdateFeeItemCommand(appraisalId, feeId, itemId, "XX", "legacy urgent", 150m)));
    }

    [Theory]
    [InlineData(500, 0, true)]   // the first item by item number is priced
    [InlineData(0, 500, false)]  // the first item is unpriced
    public async Task PricedRule_IsTheFirstItemByItemNumber_InThePreviewAndInTheAutoAssignReader(
        decimal firstPrice, decimal secondPrice, bool expected)
    {
        var (appraisalId, assignmentId) = await SeedAsync();
        await AddFeeAsync(assignmentId, sellingPrice: 1_000_000m);
        var winner = Guid.NewGuid();
        Guid companyQuotationId;
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var now = DateTime.Now;
            var rfq = QuotationRequest.Create(now.AddDays(1), "admin", now);
            rfq.SetQuotationNumber($"RFQ-{Guid.NewGuid():N}"[..14]);
            rfq.AddAppraisal(appraisalId, "admin", now);
            var won = CompanyQuotation.Create(rfq.Id, Guid.NewGuid(), winner, "QT-IT", now);
            won.AddItem(Guid.NewGuid(), appraisalId, 2, secondPrice, 3); // added first, numbered second
            won.AddItem(Guid.NewGuid(), appraisalId, 1, firstPrice, 3);
            won.MarkAsWinner();
            rfq.AddQuotation(won);
            typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.Status))!.SetValue(rfq, "Finalized");
            typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.SelectedCompanyId))!.SetValue(rfq, winner);
            typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.SelectedAt))!.SetValue(rfq, now);
            db.QuotationRequests.Add(rfq);
            await db.SaveChangesAsync(Ct);
            companyQuotationId = won.Id;
        }

        using var read = CreateScope();
        var preview = await read.ServiceProvider.GetRequiredService<ISender>()
            .Send(new GetFeePreviewQuery(appraisalId, "external", "quotation", winner), Ct);
        var priced = await read.ServiceProvider.GetRequiredService<IQuotationAutoAssignStateReader>()
            .GetPricedAppraisalIdsAsync(companyQuotationId, Ct);

        Assert.Equal(expected, preview.SystemBaseAmount is not null);
        Assert.Equal(expected, priced.Contains(appraisalId));
    }

    [Fact]
    public async Task EngagementFee_ForAProgressivePrior_IsTheKeyedInspectionFeeOnly_NeverItsOwn01()
    {
        // The collateral engagement feeds the next inspection's fee; it must follow the same rule as assignment.
        var (priorId, priorAssignment) = await SeedAsync("Progressive", assigned: true);
        Guid feeId;
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var fee = AppraisalFee.Create(priorAssignment, feePaymentType: "01", totalSellingPrice: 1m);
            fee.AddItem("01", "own inspection fee", 2_500m);
            db.AppraisalFees.Add(fee);
            await db.SaveChangesAsync(Ct);
            feeId = fee.Id;
        }

        var without = await SendAsync(new GetAppraisalForCollateralQuery(priorId));
        Assert.Null(without!.ConstructionInspectionFeeAmount); // not 2,500

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            (await db.AppraisalFees.SingleAsync(f => f.Id == feeId, Ct)).SetConstructionInspectionFee(3_000m);
            await db.SaveChangesAsync(Ct);
        }

        var keyed = await SendAsync(new GetAppraisalForCollateralQuery(priorId));
        Assert.Equal(3_000m, keyed!.ConstructionInspectionFeeAmount);
    }

    [Fact]
    public async Task AdminFee_FirstItemCreatesTheFeeRow_AndAssignmentKeepsTheAdminBase()
    {
        var (appraisalId, assignmentId) = await SeedAsync();   // admin stage, and no fee row at all

        var added = await SendAsync(new AddFeeItemCommand(appraisalId, null, "01", "Appraisal fee", 3_500m));
        var extra = await SendAsync(new AddFeeItemCommand(appraisalId, added.FeeId, "02", "Travel", 500m));

        var items = await ItemsOfAsync(appraisalId);
        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal(FeeItemSource.Admin, i.Source));
        Assert.All(items, i => Assert.False(i.RequiresApproval));

        var preview = await SendAsync(new GetFeePreviewQuery(appraisalId, "internal", "manual", null));
        Assert.Equal(3_500m, preview.AdminOverrideAmount);
        Assert.Equal(4_280m, preview.Total);

        // Hand the case over; the system's tier fee must not replace the admin's base.
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var assignment = await db.AppraisalAssignments.SingleAsync(a => a.Id == assignmentId, Ct);
            assignment.Assign("Internal", assigneeUserId: "U1", assignedBy: "test");
            await scope.ServiceProvider.GetRequiredService<IAssignmentFeeService>().EnsureAssignmentFeeItemsAsync(
                appraisalId, assignmentId, new AssignmentFeeSource.TierBased(), repriceSystemBase: true, Ct);
            await db.SaveChangesAsync(Ct);
        }

        var after = await ItemsOfAsync(appraisalId);
        Assert.Equal(2, after.Count);
        Assert.Equal(3_500m, after.Single(i => i.FeeCode == "01").FeeAmount);

        // ...and from now on the assignee cannot touch what the admin set.
        var locked = await Assert.ThrowsAsync<ConflictException>(() =>
            SendAsync(new RemoveFeeItemCommand(appraisalId, added.FeeId, after.Single(i => i.FeeCode == "02").Id)));
        Assert.Equal("FeeItemSetByAdmin", locked.Code);
        await Assert.ThrowsAsync<ConflictException>(() =>
            SendAsync(new UpdateFeeItemCommand(appraisalId, added.FeeId, after.Single(i => i.FeeCode == "01").Id, "01", "x", 1m)));
    }

    [Fact]
    public async Task AssignmentDefaults_Progressive_ReportsThePriorsCompany()
    {
        Guid companyId;
        using (var scope = CreateScope())
        {
            var auth = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var company = Company.Create($"Prev Co {Guid.NewGuid():N}"[..20]);
            auth.Companies.Add(company);
            await auth.SaveChangesAsync(Ct);
            companyId = company.Id;
        }
        var (priorId, _) = await SeedAsync(assigned: true, companyId: companyId.ToString(), number: "AP-PREV-CO");
        var (appraisalId, _) = await SeedAsync("Progressive", prevId: priorId);

        var defaults = await SendAsync(new GetAssignmentDefaultsQuery(appraisalId));

        Assert.NotNull(defaults.PreviousCompany);
        Assert.Equal(companyId, defaults.PreviousCompany!.CompanyId);
        Assert.Equal(priorId, defaults.PreviousCompany.AppraisalId);
        Assert.Equal("AP-PREV-CO", defaults.PreviousCompany.AppraisalNumber);
        Assert.True(defaults.PreviousCompany.MouActive);
    }
}
