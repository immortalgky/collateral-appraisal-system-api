using Appraisal.Application.EventHandlers;
using Appraisal.Application.Features.Appraisals.AddPropertyToGroup;
using Appraisal.Application.Features.Appraisals.CreatePropertyGroup;
using Appraisal.Application.Features.Appraisals.DeletePropertyGroup;
using Appraisal.Application.Features.Appraisals.MovePropertyToGroup;
using Appraisal.Application.Features.Appraisals.ReorderPropertiesInGroup;
using Appraisal.Application.Features.Appraisals.UpdatePropertyGroup;
using Appraisal.Application.Features.Assignments.SetOfflineExternalEngagement;
using Appraisal.Application.Features.PricingAnalysis.UpdatePricingAnalysis;
using Appraisal.Application.Features.Project.CalculateProjectUnitPrices;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Projects;
using Appraisal.Infrastructure;
using Auth.Domain.Companies;
using Auth.Infrastructure;
using Integration.Fixtures;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Request.Contracts.Requests.Dtos;
using Shared.Messaging.Events;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;
using PricingAnalysisAggregate = Appraisal.Domain.Appraisals.PricingAnalysis;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// Group commands, assignment writers (command + the three consumers), the pricing-save event path, block
/// unit pricing, and AppraisalCreationService (normal + CI/progressive). After the change none of them
/// calls UpdateAsync on the tracked aggregate except the four assignment writers, which keep it on purpose to
/// stamp Appraisals.UpdatedAt; change tracking plus the pipeline's / consumer's own SaveChanges persists the rest.
/// </summary>
[Collection("Integration")]
public class AffectedHandlersGroupAssignmentTests(IntegrationTestFixture fixture) : AffectedHandlersBase(fixture)
{
    private static AppraisalProperty AddBuilding(AppraisalAggregate a, decimal price = 100_000m, string owner = "owner")
    {
        var p = a.AddBuildingProperty();
        p.BuildingDetail!.Update(ownerName: owner);
        p.BuildingDetail.AddDepreciationDetail("Gross", isBuilding: true, priceAfterDepreciation: price);
        return p;
    }

    // ── groups ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePropertyGroup_adds_the_group_and_keeps_the_existing_one_with_its_members()
    {
        var (aid, groups, ids) = await SeedGroupedAsync(a => [AddBuilding(a)]);

        var result = await SendAsync(new CreatePropertyGroupCommand(aid, "Second", "desc"));

        var loaded = await LoadAsync(aid);
        Assert.Equal(2, loaded.Groups.Count);
        var created = Assert.Single(loaded.Groups, g => g.Id == result.Id);
        Assert.Equal(("Second", "desc", 2), (created.GroupName, created.Description, created.GroupNumber));
        Assert.Equal(1, Assert.Single(loaded.Groups, g => g.Id == groups[0]).GroupNumber);
        AssertInGroup(loaded, groups[0], ids[0], 1);
    }

    [Fact]
    public async Task UpdatePropertyGroup_renames_the_group()
    {
        var (aid, groups, ids) = await SeedGroupedAsync(a => [AddBuilding(a)]);

        await SendAsync(new UpdatePropertyGroupCommand(aid, groups[0], "Renamed", "new desc"));

        var loaded = await LoadAsync(aid);
        var group = Assert.Single(loaded.Groups);
        Assert.Equal(("Renamed", "new desc"), (group.GroupName, group.Description));
        AssertInGroup(loaded, groups[0], ids[0], 1);
    }

    [Fact]
    public async Task DeletePropertyGroup_removes_the_empty_group_its_pricing_row_and_recomputes_insurance()
    {
        var (aid, groups, ids) = await SeedGroupedAsync(a => [AddBuilding(a)], groups: 2);
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            db.PricingAnalyses.Add(PricingAnalysisAggregate.CreateForPropertyGroup(groups[1]));
            await db.SaveChangesAsync(Ct);
        }

        await SendAsync(new DeletePropertyGroupCommand(aid, groups[1]));

        var loaded = await LoadAsync(aid);
        var left = Assert.Single(loaded.Groups);
        Assert.Equal(groups[0], left.Id);
        AssertInGroup(loaded, groups[0], ids[0], 1);
        using var check = CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        Assert.False(await checkDb.PricingAnalyses.AnyAsync(p => p.AnchorId == groups[1], Ct));
        Assert.Equal(100_000m, await InsuranceAsync(aid));   // RecomputeAsync saw the loaded properties
    }

    [Fact]
    public async Task AddPropertyToGroup_puts_an_ungrouped_property_into_the_group()
    {
        var (aid2, groups, ids2) = await SeedGroupedAsync(a => [AddBuilding(a)], groups: 1, placeInGroup: false);
        var groupId = groups[0];

        var result = await SendAsync(new AddPropertyToGroupCommand(aid2, groupId, ids2[0]));

        Assert.True(result.Success);
        AssertInGroup(await LoadAsync(aid2), groupId, ids2[0], 1);
    }

    [Fact]
    public async Task MovePropertyToGroup_moves_the_member_to_the_target_position()
    {
        var (aid, groups, ids) = await SeedGroupedAsync(a => [AddBuilding(a), AddBuilding(a)], groups: 2);

        await SendAsync(new MovePropertyToGroupCommand(aid, ids[1], groups[1], 1));

        var loaded = await LoadAsync(aid);
        AssertInGroup(loaded, groups[0], ids[0], 1);
        Assert.DoesNotContain(loaded.Groups.Single(g => g.Id == groups[0]).Items, i => i.AppraisalPropertyId == ids[1]);
        AssertInGroup(loaded, groups[1], ids[1], 1);
    }

    [Fact]
    public async Task ReorderPropertiesInGroup_persists_the_new_order()
    {
        var (aid, groups, ids) = await SeedGroupedAsync(a => [AddBuilding(a), AddBuilding(a), AddBuilding(a)]);

        await SendAsync(new ReorderPropertiesInGroupCommand(aid, groups[0], [ids[2], ids[0], ids[1]]));

        var loaded = await LoadAsync(aid);
        AssertInGroup(loaded, groups[0], ids[2], 1);
        AssertInGroup(loaded, groups[0], ids[0], 2);
        AssertInGroup(loaded, groups[0], ids[1], 3);
    }

    // ── assignment ─────────────────────────────────────────────────────────────────────────

    private async Task<(Guid AppraisalId, Guid AssignmentId)> SeedAssignmentAsync(Action<AppraisalAssignment>? configure = null)
    {
        Guid assignmentId = Guid.Empty;
        var aid = await SeedAsync(a => { var assignment = a.AssignAdmin(); configure?.Invoke(assignment); });
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        assignmentId = (await db.AppraisalAssignments.AsNoTracking().SingleAsync(x => x.AppraisalId == aid, Ct)).Id;
        return (aid, assignmentId);
    }

    private async Task<AppraisalAssignment> AssignmentAsync(Guid aid)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        return await db.AppraisalAssignments.AsNoTracking().SingleAsync(x => x.AppraisalId == aid, Ct);
    }

    private async Task<DateTime?> AppraisalUpdatedAtAsync(Guid aid)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        return (await db.Appraisals.AsNoTracking().SingleAsync(x => x.Id == aid, Ct)).UpdatedAt;
    }

    private async Task AssertUpdatedAtMovedAsync(Guid aid, DateTime? seeded)
    {
        var after = await AppraisalUpdatedAtAsync(aid);
        Assert.NotNull(after);
        Assert.True(seeded is null || after > seeded, $"Appraisals.UpdatedAt did not move: {seeded} -> {after}");
    }

    private static ConsumeContext<T> Context<T>(T message) where T : class
    {
        var ctx = Substitute.For<ConsumeContext<T>>();
        ctx.MessageId.Returns(Guid.NewGuid());
        ctx.Message.Returns(message);
        ctx.CancellationToken.Returns(Ct);
        return ctx;
    }

    private async Task ConsumeAsync<TConsumer, TMessage>(TMessage message)
        where TConsumer : class, IConsumer<TMessage> where TMessage : class
    {
        using var scope = CreateScope();
        var consumer = ActivatorUtilities.CreateInstance<TConsumer>(scope.ServiceProvider);
        await consumer.Consume(Context(message));
    }

    [Fact]
    public async Task InternalAssignedIntegrationEventHandler_persists_the_internal_assignment()
    {
        var (aid, _) = await SeedAssignmentAsync();
        var stamp = await AppraisalUpdatedAtAsync(aid);

        await ConsumeAsync<InternalAssignedIntegrationEventHandler, InternalAssignedIntegrationEvent>(
            new InternalAssignedIntegrationEvent
            {
                AppraisalId = aid, AssigneeUserId = "user-1", InternalAppraiserId = "ia-1", AssignmentMethod = "Manual"
            });

        var assignment = await AssignmentAsync(aid);
        Assert.Equal("Internal", assignment.AssignmentType.Code);
        Assert.Equal("user-1", assignment.AssigneeUserId);
        Assert.Equal("ia-1", assignment.InternalAppraiserId);
        Assert.Equal("Manual", assignment.AssignmentMethod);
        Assert.Equal(AssignmentStatus.InProgress.Code, assignment.AssignmentStatus.Code);
        Assert.NotNull(assignment.AssignedAt);
        await AssertUpdatedAtMovedAsync(aid, stamp);
    }

    [Fact]
    public async Task InternalFollowupAssignedIntegrationEventHandler_persists_the_followup_fields()
    {
        var (aid, _) = await SeedAssignmentAsync();
        var stamp = await AppraisalUpdatedAtAsync(aid);

        await ConsumeAsync<InternalFollowupAssignedIntegrationEventHandler, InternalFollowupAssignedIntegrationEvent>(
            new InternalFollowupAssignedIntegrationEvent
            {
                AppraisalId = aid, InternalAppraiserId = "followup-1", InternalFollowupAssignmentMethod = "RoundRobin"
            });

        var assignment = await AssignmentAsync(aid);
        Assert.Equal("followup-1", assignment.InternalAppraiserId);
        Assert.Equal("RoundRobin", assignment.InternalFollowupAssignmentMethod);
        await AssertUpdatedAtMovedAsync(aid, stamp);
    }

    [Fact]
    public async Task CompanyAssignedIntegrationEventHandler_persists_the_external_assignment()
    {
        var (aid, _) = await SeedAssignmentAsync();
        var stamp = await AppraisalUpdatedAtAsync(aid);
        var companyId = Guid.NewGuid();

        await ConsumeAsync<CompanyAssignedIntegrationEventHandler, CompanyAssignedIntegrationEvent>(
            new CompanyAssignedIntegrationEvent
            {
                AppraisalId = aid, CompanyId = companyId, CompanyName = "QA Co", AssignmentMethod = "Manual"
            });

        var assignment = await AssignmentAsync(aid);
        Assert.Equal("External", assignment.AssignmentType.Code);
        Assert.Equal(companyId.ToString(), assignment.AssigneeCompanyId);
        Assert.Equal("Manual", assignment.AssignmentMethod);
        Assert.Equal(AssignmentStatus.Assigned.Code, assignment.AssignmentStatus.Code);
        await AssertUpdatedAtMovedAsync(aid, stamp);
    }

    [Fact]
    public async Task SetOfflineExternalEngagement_persists_company_status_book_date_and_external_appraiser()
    {
        var (aid, _) = await SeedAssignmentAsync(a =>
            a.SaveDraft("Internal", "keyer-1", null, AppraisalAssignment.OfflineAssignmentMethod, null, null, null));
        var stamp = await AppraisalUpdatedAtAsync(aid);
        Guid companyId;
        using (var scope = CreateScope())
        {
            var auth = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var company = Company.Create($"QA Co {Guid.NewGuid():N}"[..20]);
            auth.Companies.Add(company);
            await auth.SaveChangesAsync(Ct);
            companyId = company.Id;
        }
        var bookDate = new DateTime(2026, 3, 4);

        var result = await SendAsync(new SetOfflineExternalEngagementCommand(aid, companyId, bookDate, "Ext Appraiser", "qa"));

        var assignment = await AssignmentAsync(aid);
        Assert.Equal(result.AssignmentId, assignment.Id);
        Assert.Equal("External", assignment.AssignmentType.Code);
        Assert.Equal(companyId.ToString(), assignment.AssigneeCompanyId);
        Assert.Equal("keyer-1", assignment.AssigneeUserId);
        Assert.Equal(AppraisalAssignment.OfflineAssignmentMethod, assignment.AssignmentMethod);
        Assert.Equal(AssignmentStatus.InProgress.Code, assignment.AssignmentStatus.Code);
        Assert.Equal("Ext Appraiser", assignment.ExternalAppraiserName);
        using var check = CreateScope();
        var row = await check.ServiceProvider.GetRequiredService<AppraisalDbContext>()
            .ValuationAnalyses.AsNoTracking().SingleAsync(v => v.AppraisalId == aid, Ct);
        Assert.Equal(bookDate, row.ValuationDate);
        await AssertUpdatedAtMovedAsync(aid, stamp);
    }

    // ── pricing event path ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pricing_final_value_save_persists_the_analysis_and_the_valuation_summary_with_insurance()
    {
        var (aid, groups, _) = await SeedGroupedAsync(a => [AddBuilding(a), AddBuilding(a, 50_000m)]);
        Guid analysisId;
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var analysis = PricingAnalysisAggregate.CreateForPropertyGroup(groups[0]);
            db.PricingAnalyses.Add(analysis);
            await db.SaveChangesAsync(Ct);
            analysisId = analysis.Id;
        }

        // UpdatePricingAnalysisCommand -> SetFinalValues -> AppraisalFinalValuesChangedEvent, dispatched pre-save.
        await SendAsync(new UpdatePricingAnalysisCommand(analysisId, null, 1_500_000m, null, null));

        using var check = CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        Assert.Equal(1_500_000m, (await db2.PricingAnalyses.AsNoTracking().SingleAsync(p => p.Id == analysisId, Ct)).FinalAppraisedValue);
        var row = await db2.ValuationAnalyses.AsNoTracking().SingleAsync(v => v.AppraisalId == aid, Ct);
        Assert.Equal(1_500_000m, row.AppraisedValue);
        Assert.Equal(150_000m, row.InsuranceValue);
    }

    [Fact]
    public async Task CalculateProjectUnitPrices_persists_unit_prices_and_the_block_valuation_summary()
    {
        var aid = await SeedAsync(_ => { });
        Guid unitId;
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var project = Project.Create(aid, ProjectType.LandAndBuilding, projectName: "QA block");
            var unit = ProjectUnit.CreateLandAndBuilding(project.Id, Guid.Empty, 1, plotNumber: "P1", modelType: "M1",
                landArea: 20m, usableArea: 100m);
            project.ImportUnits("units.csv", null, [unit]);
            project.SetLandAndBuildingPricingAssumption(null, null, null, null, null, 1_000m, 70m);
            db.Projects.Add(project);
            // The model's standard price is its pricing analysis' final value (2,000,000). The model's
            // standard land area is the units' own area, so the land adjustment is 0.
            var modelAnalysis = PricingAnalysisAggregate.CreateForProjectModel(project.Models.Single().Id);
            typeof(PricingAnalysisAggregate).GetProperty(nameof(PricingAnalysisAggregate.FinalAppraisedValue))!
                .SetValue(modelAnalysis, 2_000_000m);
            db.PricingAnalyses.Add(modelAnalysis);
            await db.SaveChangesAsync(Ct);
            unitId = unit.Id;
        }

        await SendAsync(new CalculateProjectUnitPricesCommand(aid));

        using var check = CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var price = await db2.ProjectUnitPrices.AsNoTracking().SingleAsync(p => p.ProjectUnitId == unitId, Ct);
        Assert.Equal(2_000_000m, price.TotalAppraisalValueRounded);
        Assert.Equal(1_400_000m, price.ForceSellingPrice);         // 70%
        var row = await db2.ValuationAnalyses.AsNoTracking().SingleAsync(v => v.AppraisalId == aid, Ct);
        Assert.Equal(2_000_000m, row.AppraisedValue);

        // Re-running upserts the same row instead of adding a second one.
        await SendAsync(new CalculateProjectUnitPricesCommand(aid));
        using var again = CreateScope();
        Assert.Equal(1, await again.ServiceProvider.GetRequiredService<AppraisalDbContext>()
            .ProjectUnitPrices.CountAsync(p => p.ProjectUnitId == unitId, Ct));
    }

    // ── AppraisalCreationService ───────────────────────────────────────────────────────────

    private async Task<Guid> CreateFromRequestAsync(Guid requestId, List<RequestTitleDto> titles,
        AppointmentDto? appointment = null, Guid? prevAppraisalId = null, string? appraisalType = null)
    {
        using var scope = CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAppraisalCreationService>().CreateAppraisalFromRequest(
            requestId, titles, [], appointment, createdBy: "qa", priority: "Normal", requestedBy: "qa",
            prevAppraisalId: prevAppraisalId, appraisalType: appraisalType, cancellationToken: Ct);
    }

    [Fact]
    public async Task AppraisalCreationService_normal_path_persists_appraisal_properties_group_and_assignment()
    {
        var requestId = Guid.NewGuid();
        var id = await CreateFromRequestAsync(requestId,
        [
            new RequestTitleDto { CollateralType = "01", TitleNumber = "QA-100", OwnerName = "Land owner" },
            new RequestTitleDto { CollateralType = "08", TitleNumber = "QA-200", OwnerName = "Condo owner", CondoName = "QA Tower" },
        ], new AppointmentDto(new DateTime(2026, 5, 5, 9, 0, 0), "site"));

        var loaded = await LoadAsync(id);
        Assert.Equal(requestId, loaded.RequestId);
        Assert.Equal(2, loaded.Properties.Count);
        Assert.Contains(loaded.Properties, p => p.PropertyType == PropertyType.Land);
        Assert.Contains(loaded.Properties, p => p.PropertyType == PropertyType.Condo);
        Assert.Equal(2, loaded.Groups.Count);                         // one default group per family
        Assert.All(loaded.Properties, p => Assert.Single(loaded.Groups, g => g.Items.Any(i => i.AppraisalPropertyId == p.Id)));
        var assignment = await AssignmentAsync(id);
        Assert.Equal(AssignmentStatus.Pending.Code, assignment.AssignmentStatus.Code);
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        Assert.Single(await db.Appointments.AsNoTracking().Where(x => x.AssignmentId == assignment.Id).ToListAsync(Ct));
    }

    [Fact]
    public async Task AppraisalCreationService_progressive_path_copies_properties_groups_pricing_and_recomputes_the_summary()
    {
        // Prior appraisal: one building (insurance 100,000) in a group whose pricing analysis is worth 1,000,000.
        var (priorId, priorGroups, _) = await SeedGroupedAsync(a => [AddBuilding(a, 100_000m, "Prior owner")]);
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var analysis = PricingAnalysisAggregate.CreateForPropertyGroup(priorGroups[0]);
            // Set by reflection: SetFinalValues would also raise the pre-save summary event while seeding.
            typeof(PricingAnalysisAggregate).GetProperty(nameof(PricingAnalysisAggregate.FinalAppraisedValue))!
                .SetValue(analysis, 1_000_000m);
            db.PricingAnalyses.Add(analysis);
            await db.SaveChangesAsync(Ct);
        }
        var apptDate = new DateTime(2026, 5, 5, 9, 0, 0);

        var id = await CreateFromRequestAsync(Guid.NewGuid(), [], new AppointmentDto(apptDate, "site"),
            prevAppraisalId: priorId, appraisalType: AppraisalTypes.Progressive);

        var loaded = await LoadAsync(id);
        var copy = Assert.Single(loaded.Properties);
        Assert.Equal("Prior owner", copy.BuildingDetail!.OwnerName);
        Assert.Equal(100_000m, Assert.Single(copy.BuildingDetail.DepreciationDetails).PriceAfterDepreciation);
        var group = Assert.Single(loaded.Groups);
        AssertInGroup(loaded, group.Id, copy.Id, 1);

        using var check = CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var clone = await db2.PricingAnalyses.AsNoTracking().SingleAsync(p => p.AnchorId == group.Id, Ct);
        Assert.Equal(1_000_000m, clone.FinalAppraisedValue);
        var row = await db2.ValuationAnalyses.AsNoTracking().SingleAsync(v => v.AppraisalId == id, Ct);
        Assert.Equal(1_000_000m, row.AppraisedValue);
        Assert.Equal(100_000m, row.InsuranceValue);                    // sum over the repository-loaded copy
        Assert.Equal(apptDate, row.ValuationDate);                     // explicit appointment date, not Now
        Assert.Equal(priorId, loaded.PrevAppraisalId);
    }
}
