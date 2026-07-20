using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Collateral.CollateralMasters.Consumers;
using Collateral.CollateralMasters.Models;
using Collateral.CollateralMasters.Services;
using Collateral.Contracts.ConstructionInspection;
using Collateral.Contracts.Engagements;
using Collateral.Data;
using Integration.Fixtures;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using System.Text.Json;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;
using Address = Appraisal.Domain.Appraisals.Address;

namespace Integration.Collateral.Integration.Tests;

/// <summary>
/// PR-6 caller-chain integration tests.
///
/// These tests cover the seams *outside* the Collateral module that feed into
/// Collateral upserts — the gaps left after PR-1 through PR-5 hardened the
/// Collateral write path itself.
///
/// Test inventory:
///   PR6-1  Contract test: for an Appeal request the consumer resolves
///          GetMostRecentEngagementByPriorAppraisalQuery and EXCLUDES the company,
///          so the Workflow → Collateral read-side seam is covered.
///   PR6-2  Contract test: for a Progressive (construction inspection) request the
///          consumer resolves GetMostRecentEngagementByPriorAppraisalQuery to FORCE
///          the company and pick the copy/fee source — that seam is covered.
///   PR6-3  AssignmentFeeService.ResolveSourceForAppraisalAsync pulls CI fee
///          from a seeded engagement for the prior appraisal ID.
///   PR6-4  End-to-end: AppraisalCompletedConsumer → ProcessAppraisalAsync →
///          one engagement created, primary IsMaster anchored, snapshot groups[].
///   PR6-5  End-to-end: Land alias-alone appraisal → consumer succeeds, engagement
///          anchored on parent IsMaster, master row classification unchanged
///          (graceful behavior — PR-7 removed the alias-alone exception).
/// </summary>
[Collection("Integration")]
public class CollateralPR6_CallerChainTests(IntegrationTestFixture fixture)
{
    // -----------------------------------------------------------------------
    // Seed helpers (mirroring existing PR-4 / PR-5 test patterns)
    // -----------------------------------------------------------------------

    private static AppraisalAggregate CreateAppraisalSeed(Guid requestId, string prefix = "PR6")
    {
        var a = AppraisalAggregate.Create(requestId, "New", "Normal", DateTime.Now);
        a.SetAppraisalNumber($"AP-{prefix}-{Guid.NewGuid():N}"[..18]);
        typeof(AppraisalAggregate)
            .GetProperty("CompletedAt")!
            .SetValue(a, DateTime.UtcNow);
        return a;
    }

    private static AppraisalProperty SeedLandProperty(
        AppraisalAggregate appraisal,
        string landOffice, string province, string district, string subDistrict,
        string titleNo, string titleType)
    {
        var prop = appraisal.AddLandProperty();
        prop.LandDetail!.Update(
            address: Address.Create(subDistrict, district, province), landOffice: landOffice);
        var title = LandTitle.Create(prop.LandDetail.Id, titleNo, titleType);
        prop.LandDetail.AddTitle(title);
        return prop;
    }

    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private CollateralDbContext GetCollateralDbContext(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<CollateralDbContext>();

    private AppraisalDbContext GetAppraisalDbContext(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

    private async Task ProcessAppraisalInNewScopeAsync(Guid appraisalId)
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICollateralMasterUpsertService>();
        await svc.ProcessAppraisalAsync(appraisalId, TestContext.Current.CancellationToken);
    }

    // -----------------------------------------------------------------------
    // Helper: build a minimal fake MassTransit ConsumeContext<T> from the
    // MassTransit.Testing package's ConsumeContext stub.
    // We use MassTransit's built-in InMemoryTestHarness approach: register the
    // consumer under test via AddMassTransitTestHarness, publish the message,
    // and let the harness route it to the consumer registered in DI.
    //
    // For the contract tests (PR6-1, PR6-2) we skip the full harness because
    // RequestSubmittedIntegrationEventConsumer depends on heavy Workflow
    // infrastructure (IWorkflowDefinitionRepository, etc.) that is outside the
    // Collateral module boundary. Instead we exercise the MediatR handlers that
    // the consumer delegates to — this directly covers the "seam" that the
    // consumer exploits.
    // -----------------------------------------------------------------------

    // -----------------------------------------------------------------------
    // PR6-1: Workflow → Collateral read-side seam (appeal exclusion contract)
    //
    // For an Appeal request, RequestSubmittedIntegrationEventConsumer calls
    //   mediator.Send(new GetMostRecentEngagementByPriorAppraisalQuery(prevAppraisalId))
    // and EXCLUDES the resolved company. This test seeds a CollateralMaster with an
    // engagement that carries a company ID, then sends the query directly to verify
    // the handler resolves the seeded company via the master link — the same call
    // path the workflow consumer uses.
    //
    // Why this approach: the workflow consumer requires the full Workflow module
    // (WorkflowInstance, WorkflowDefinition, outbox, etc.). Calling the MediatR
    // handler directly covers the Collateral side of the cross-module boundary
    // without introducing coupling to the Workflow module's heavy DI graph.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PR6_1_WorkflowAppraisalExclusion_QueryReturnsSeededCompany()
    {
        var titleNo = "PR6-1-" + Guid.NewGuid().ToString("N")[..8];
        var companyId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        // Seed: appraisal → ProcessAppraisalAsync creates the master + engagement
        Guid appraisalId;
        using (var seed = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seed);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a, "LO-001", "Bangkok", "Bangrak", "Silom", titleNo, "Chanote");
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(ct);
            appraisalId = a.Id;
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        // Patch the engagement to carry an appraisal company ID so the handler
        // has something to return (the upsert service seeds company from
        // AppraisalFee which is not wired in isolation; we patch directly).
        using (var patch = CreateScope())
        {
            var db = GetCollateralDbContext(patch);
            var engagement = await db.CollateralEngagements
                .FirstAsync(e => e.AppraisalId == appraisalId, ct);

            // Patch via reflection — same technique used in other PR tests
            typeof(CollateralEngagement)
                .GetProperty("AppraisalCompanyId")!
                .SetValue(engagement, companyId);

            await db.SaveChangesAsync(ct);
        }

        // Act: invoke the MediatR handler the workflow consumer uses
        using var assert = CreateScope();
        var mediator = assert.ServiceProvider.GetRequiredService<ISender>();

        var result = await mediator.Send(
            new GetMostRecentEngagementByPriorAppraisalQuery(appraisalId), ct);

        // Assert: handler resolves the engagement carrying the company → the seam is wired
        Assert.NotNull(result);
        Assert.Equal(companyId, result!.CompanyId);
    }

    // -----------------------------------------------------------------------
    // PR6-2: Workflow → Collateral read-side seam (Progressive force-company contract)
    //
    // For a Progressive (construction inspection) request, the consumer calls
    //   mediator.Send(new GetMostRecentEngagementByPriorAppraisalQuery(prevAppraisalId))
    // to pin the request to the prior company (and resolve the copy/fee source).
    //
    // This test seeds an engagement with a known company ID for a prior appraisal,
    // then sends the query to verify the handler returns the resolved EngagementRef
    // (AppraisalId + CompanyId + CompanyName).
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PR6_2_CIForceCompany_QueryReturnsSeededCompanyFromEngagement()
    {
        var titleNo = "PR6-2-" + Guid.NewGuid().ToString("N")[..8];
        var companyId = Guid.NewGuid();
        const string companyName = "Test Appraisal Co. Ltd.";
        var ct = TestContext.Current.CancellationToken;

        Guid appraisalId;
        using (var seed = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seed);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a, "LO-002", "Chiang Mai", "Mueang", "Wat Gate", titleNo, "NorSor4Jor");
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(ct);
            appraisalId = a.Id;
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId);

        // Patch engagement with company data
        using (var patch = CreateScope())
        {
            var db = GetCollateralDbContext(patch);
            var engagement = await db.CollateralEngagements
                .FirstAsync(e => e.AppraisalId == appraisalId, ct);

            typeof(CollateralEngagement)
                .GetProperty("AppraisalCompanyId")!
                .SetValue(engagement, companyId);
            typeof(CollateralEngagement)
                .GetProperty("AppraisalCompanyName")!
                .SetValue(engagement, companyName);

            await db.SaveChangesAsync(ct);
        }

        // Act: invoke the MediatR handler the workflow consumer uses for CI routing
        using var assert = CreateScope();
        var mediator = assert.ServiceProvider.GetRequiredService<ISender>();

        var result = await mediator.Send(
            new GetMostRecentEngagementByPriorAppraisalQuery(appraisalId), ct);

        // Assert: returns the resolved engagement reference
        Assert.NotNull(result);
        Assert.Equal(appraisalId, result!.AppraisalId);
        Assert.Equal(companyId, result.CompanyId);
        Assert.Equal(companyName, result.CompanyName);
    }

    // -----------------------------------------------------------------------
    // PR6-3: AssignmentFeeService CI fee read-back
    //
    // AssignmentFeeService.ResolveSourceForAppraisalAsync (line ~191) sends
    // GetConstructionInspectionFeeForAppraisalQuery(prevId) to retrieve the
    // CI fee amount captured on the prior appraisal's engagement.
    //
    // Setup: create a prior appraisal, run ProcessAppraisalAsync, patch the
    // engagement's ConstructionInspectionFeeAmount, then call the service.
    //
    // Assert: the service returns AssignmentFeeSource.ConstructionInspection
    // with the exact amount seeded on the engagement.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PR6_3_AssignmentFeeService_CIFeeReadBack_ReturnsSeedFeeAmount()
    {
        var titleNo = "PR6-3-" + Guid.NewGuid().ToString("N")[..8];
        const decimal seededCIFee = 45_000m;
        var ct = TestContext.Current.CancellationToken;

        // Create and process a prior appraisal (the "base" appraisal for a CI request)
        Guid priorAppraisalId;
        using (var seed = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seed);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a, "LO-003", "Bangkok", "Sathorn", "Yannawa", titleNo, "Chanote");
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(ct);
            priorAppraisalId = a.Id;
        }

        await ProcessAppraisalInNewScopeAsync(priorAppraisalId);

        // Patch engagement with CI fee amount
        using (var patch = CreateScope())
        {
            var db = GetCollateralDbContext(patch);
            var engagement = await db.CollateralEngagements
                .FirstAsync(e => e.AppraisalId == priorAppraisalId, ct);

            typeof(CollateralEngagement)
                .GetProperty("ConstructionInspectionFeeAmount")!
                .SetValue(engagement, seededCIFee);

            await db.SaveChangesAsync(ct);
        }

        // Create a new CI appraisal that references the prior appraisal
        Guid ciAppraisalId;
        using (var seed = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seed);
            var ciAppraisal = AppraisalAggregate.Create(
                Guid.NewGuid(),
                AppraisalTypes.Progressive,
                "Normal",
                DateTime.Now,
                prevAppraisalId: priorAppraisalId);
            ciAppraisal.SetAppraisalNumber("AP-CI-TEST-001");

            typeof(AppraisalAggregate)
                .GetProperty("CompletedAt")!
                .SetValue(ciAppraisal, DateTime.UtcNow);

            appraisalDb.Appraisals.Add(ciAppraisal);
            await appraisalDb.SaveChangesAsync(ct);
            ciAppraisalId = ciAppraisal.Id;
        }

        // Act: call the MediatR query directly (same query the service sends)
        using var assert = CreateScope();
        var mediator = assert.ServiceProvider.GetRequiredService<ISender>();

        var fee = await mediator.Send(
            new GetConstructionInspectionFeeForAppraisalQuery(priorAppraisalId), ct);

        // Assert: handler returns the seeded CI fee amount
        Assert.Equal(seededCIFee, fee);

        // Also verify that AssignmentFeeService itself resolves to
        // ConstructionInspection source with the correct amount.
        var appraisalDb2 = assert.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var ciAppraisal2 = await appraisalDb2.Appraisals
            .FirstAsync(a => a.Id == ciAppraisalId, ct);

        var feeService = assert.ServiceProvider.GetRequiredService<IAssignmentFeeService>();
        var source = await feeService.ResolveSourceForAppraisalAsync(
            ciAppraisal2, new AssignmentFeeSource.TierBased(), ct);

        var ciSource = Assert.IsType<AssignmentFeeSource.ConstructionInspection>(source);
        Assert.Equal(seededCIFee, ciSource.Amount);
    }

    // -----------------------------------------------------------------------
    // PR6-4: AppraisalCompletedConsumer end-to-end — engagement created with
    //         correct structure (multi-group case).
    //
    // This tests the full consumer dispatch path: publish an
    // AppraisalCompletedIntegrationEvent → AppraisalCompletedConsumer receives
    // it and calls ProcessAppraisalAsync → exactly one CollateralEngagement
    // row is created, anchored to the primary IsMaster, with a well-formed
    // groups[] snapshot.
    //
    // Approach: directly instantiate the consumer with real DI dependencies
    // (no MassTransit bus harness needed — the consumer has a simple interface
    // that calls the upsert service). This exercises the consumer's full
    // dispatch logic while keeping the test isolated to the Collateral module.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PR6_4_AppraisalCompleted_Consumer_CreatesEngagementWithCorrectGroupsSnapshot()
    {
        var title1 = "PR6-4A-" + Guid.NewGuid().ToString("N")[..7];
        var title2 = "PR6-4B-" + Guid.NewGuid().ToString("N")[..7];
        var ct = TestContext.Current.CancellationToken;

        // Seed: appraisal with two groups → two IsMasters
        // Assign explicit Guids before AddProperty so the FK in PropertyGroupItem
        // is correct before EF assigns server-default IDs via NEWSEQUENTIALID().
        Guid appraisalId;
        using (var seed = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seed);
            var a = CreateAppraisalSeed(Guid.NewGuid());

            var g1 = a.CreateGroup("Group 1");
            var p1 = SeedLandProperty(a, "LO-001", "Bangkok", "Bangrak", "Silom", title1, "Chanote");
            p1.Id = Guid.NewGuid();
            g1.AddProperty(p1.Id);

            var g2 = a.CreateGroup("Group 2");
            var p2 = SeedLandProperty(a, "LO-002", "Chiang Mai", "Mueang", "Chang Phueak", title2, "Chanote");
            p2.Id = Guid.NewGuid();
            g2.AddProperty(p2.Id);

            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(ct);
            appraisalId = a.Id;
        }

        // Act: resolve the consumer from DI and invoke it with a minimal ConsumeContext
        using var scope = CreateScope();
        var upsertService = scope.ServiceProvider.GetRequiredService<ICollateralMasterUpsertService>();
        var collateralDb = GetCollateralDbContext(scope);
        var inboxGuard = scope.ServiceProvider.GetRequiredService<InboxGuard<CollateralDbContext>>();
        var logger = NullLogger<AppraisalCompletedConsumer>.Instance;

        var consumer = new AppraisalCompletedConsumer(upsertService, logger, inboxGuard);

        var messageId = NewId.NextGuid();
        var fakeContext = FakeConsumeContext.Build(
            new AppraisalCompletedIntegrationEvent
            {
                AppraisalId = appraisalId,
                RequestId = Guid.NewGuid(),
                CompletedAt = DateTime.UtcNow
            },
            messageId,
            ct);

        await consumer.Consume(fakeContext);

        // Assert: exactly ONE engagement across the whole appraisal
        using var assert = CreateScope();
        var db = GetCollateralDbContext(assert);

        var engagements = await db.CollateralEngagements
            .Where(e => e.AppraisalId == appraisalId)
            .ToListAsync(ct);

        Assert.Single(engagements);

        var engagement = engagements.Single();

        // Snapshot must contain two groups (one per group on the appraisal)
        Assert.NotNull(engagement.Snapshot);
        using var doc = JsonDocument.Parse(engagement.Snapshot);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("groups", out var groupsEl));
        var groupList = groupsEl.EnumerateArray().ToList();
        Assert.Equal(2, groupList.Count);

        // Exactly one group is marked isPrimary
        var primaryGroups = groupList.Count(g =>
            g.TryGetProperty("isPrimary", out var ip) && ip.ValueKind == JsonValueKind.True);
        Assert.Equal(1, primaryGroups);

        // The engagement is anchored to a master that IS the IsMaster
        var anchorMaster = await db.CollateralMasters
            .FirstAsync(m => m.Id == engagement.CollateralMasterId, ct);
        Assert.True(anchorMaster.IsMaster);
    }

    // -----------------------------------------------------------------------
    // PR6-5: Land alias re-appraisal — consumer succeeds, engagement anchored
    //         on parent IsMaster.
    //
    // Both the Land and Condo paths now behave identically for alias-alone:
    // the service resolves the alias to its parent IsMaster and proceeds.
    // (PR-7 removed AliasWithoutParentException; validation lives upstream
    // in the Request module.)
    //
    // Setup:  appraisal #1 creates a 2-title group → title1 = IsMaster, title2 = alias.
    //         appraisal #2 contains ONLY title2 (the alias).
    // Assert:
    //   - Consumer completes without error
    //   - One engagement created for appraisal #2, anchored to the IsMaster (title1's master)
    //   - IsMaster row classification has not changed (still IsMaster, no ParentMasterId)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task PR6_5_AliasLandReappraisal_ConsumerSucceeds_EngagementAnchoredOnParentIsMaster()
    {
        var title1 = "PR6-5A-" + Guid.NewGuid().ToString("N")[..7];
        var title2 = "PR6-5B-" + Guid.NewGuid().ToString("N")[..7];
        var ct = TestContext.Current.CancellationToken;

        // Appraisal #1: both titles in the same group → title1 becomes IsMaster, title2 becomes alias.
        // Assign explicit Guids before adding to the group because EF's NEWSEQUENTIALID() hasn't
        // fired yet (AppraisalProperty.Create leaves Id = Guid.Empty).
        Guid appraisalId1;
        using (var seed = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seed);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            var g = a.CreateGroup("Group AB");
            var p1 = SeedLandProperty(a, "LO-001", "Bangkok", "Bangrak", "Silom", title1, "Chanote");
            var p2 = SeedLandProperty(a, "LO-001", "Bangkok", "Bangrak", "Silom", title2, "NorSor4Jor");
            p1.Id = Guid.NewGuid();
            p2.Id = Guid.NewGuid();
            g.AddProperty(p1.Id);
            g.AddProperty(p2.Id);
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(ct);
            appraisalId1 = a.Id;
        }

        await ProcessAppraisalInNewScopeAsync(appraisalId1);

        // Capture the IsMaster row created for appraisal #1.
        // Either title1 or title2 could be the IsMaster depending on query ordering;
        // what matters is that exactly one IsMaster exists for this group.
        Guid isMasterId;
        using (var snap = CreateScope())
        {
            var db = GetCollateralDbContext(snap);
            var masters = await db.CollateralMasters
                .Include(m => m.LandDetail)
                .Where(m => m.LandDetail != null &&
                    (m.LandDetail.TitleNumber == title1 || m.LandDetail.TitleNumber == title2))
                .ToListAsync(ct);

            var isMasterRow = masters.SingleOrDefault(m => m.IsMaster);
            Assert.NotNull(isMasterRow);
            isMasterId = isMasterRow.Id;
        }

        // Appraisal #2: only the alias title (title2), parent IsMaster (title1) is absent
        Guid appraisalId2;
        using (var seed = CreateScope())
        {
            var appraisalDb = GetAppraisalDbContext(seed);
            var a = CreateAppraisalSeed(Guid.NewGuid());
            SeedLandProperty(a, "LO-001", "Bangkok", "Bangrak", "Silom", title2, "NorSor4Jor");
            appraisalDb.Appraisals.Add(a);
            await appraisalDb.SaveChangesAsync(ct);
            appraisalId2 = a.Id;
        }

        // Act: consumer should complete without error (land alias → resolved to parent IsMaster)
        using var scope = CreateScope();
        var upsertService = scope.ServiceProvider.GetRequiredService<ICollateralMasterUpsertService>();
        var inboxGuard = scope.ServiceProvider.GetRequiredService<InboxGuard<CollateralDbContext>>();
        var logger = NullLogger<AppraisalCompletedConsumer>.Instance;

        var consumer = new AppraisalCompletedConsumer(upsertService, logger, inboxGuard);
        var messageId = NewId.NextGuid();
        var fakeContext = FakeConsumeContext.Build(
            new AppraisalCompletedIntegrationEvent
            {
                AppraisalId = appraisalId2,
                RequestId = Guid.NewGuid(),
                CompletedAt = DateTime.UtcNow
            },
            messageId,
            ct);

        // Does NOT throw — the land path resolves alias → IsMaster parent and proceeds
        await consumer.Consume(fakeContext);

        // Assert: new engagement created for appraisal #2, anchored on the IsMaster
        using var assert = CreateScope();
        var assertDb = GetCollateralDbContext(assert);

        var engagementsForAppraisal2 = await assertDb.CollateralEngagements
            .Where(e => e.AppraisalId == appraisalId2)
            .ToListAsync(ct);

        Assert.Single(engagementsForAppraisal2);
        Assert.Equal(isMasterId, engagementsForAppraisal2.Single().CollateralMasterId);

        // Assert: IsMaster row classification has NOT changed
        var isMasterAfter = await assertDb.CollateralMasters
            .Include(m => m.LandDetail)
            .FirstAsync(m => m.Id == isMasterId, ct);

        Assert.True(isMasterAfter.IsMaster, "IsMaster classification must not have changed");
        Assert.True(isMasterAfter.ParentMasterId is null, "IsMaster must not have acquired a parent");
    }
}

// ---------------------------------------------------------------------------
// Factory: build a minimal NSubstitute-backed ConsumeContext<T>.
// Only MessageId, Message, and CancellationToken are accessed by the consumers
// under test. All other interface members return NSubstitute defaults (null /
// Task.CompletedTask / false) which is safe for the call paths exercised here.
// ---------------------------------------------------------------------------
internal static class FakeConsumeContext
{
    internal static ConsumeContext<T> Build<T>(T message, Guid messageId, CancellationToken ct)
        where T : class
    {
        var ctx = Substitute.For<ConsumeContext<T>>();
        ctx.MessageId.Returns(messageId);
        ctx.Message.Returns(message);
        ctx.CancellationToken.Returns(ct);
        return ctx;
    }
}
