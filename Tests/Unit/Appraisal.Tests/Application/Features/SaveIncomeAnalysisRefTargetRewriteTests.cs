using Appraisal.Domain.Appraisals.Income;
using Appraisal.Domain.Appraisals.Income.MethodDetails;
using Appraisal.Domain.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Appraisal.Tests.Application.Features;

/// <summary>
/// Tests for the Method-13 refTarget rewrite performed during SaveIncomeAnalysis.
///
/// The frontend sends a freshly-built tree where each node has a client-assigned Guid
/// (clientId) but no dbId.  The Save handler builds an idMap from clientId → new dbId
/// as it materialises the tree, then calls IncomeRefTargetRewriter.Rewrite to populate
/// each Method-13 refTarget.dbId before the calculator runs.
/// </summary>
public class SaveIncomeAnalysisRefTargetRewriteTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string Serialize(object obj)
        => JsonSerializer.Serialize(obj, JsonOpts);

    // ── helper: build a minimal domain tree that mimics what BuildSections produces ──

    /// <summary>
    /// Creates an IncomeAnalysis with two assumptions:
    ///   A (method 03, base income 1_000_000)
    ///   B (method 13, 10% of A, clientId-only ref)
    ///
    /// Entity IDs are assigned explicitly because IncomeAssumption.Create has
    /// Guid.CreateVersion7() commented out (EF sets it server-side via NEWSEQUENTIALID()).
    ///
    /// The idMap maps each node's clientId → new dbId, just as SaveIncomeAnalysisCommandHandler would.
    /// Returns (analysis, idMap, clientIdForA, dbIdForA, dbIdForB).
    /// </summary>
    private static (
        IncomeAnalysis analysis,
        Dictionary<Guid, Guid> idMap,
        Guid clientIdA,
        Guid dbIdA,
        Guid dbIdB)
        BuildFreshSaveTree()
    {
        var analysis = IncomeAnalysis.Create(
            Guid.NewGuid(), "dcf-test", "DCF Test", 3, 365, 5m, 8m);

        // Explicit runtime dbIds (stand-ins for the IDs that EF would assign via NEWSEQUENTIALID)
        var dbIdA = Guid.NewGuid();
        var dbIdB = Guid.NewGuid();

        // Simulate client-assigned Guids (what the frontend sends as ClientId)
        var clientIdA = Guid.NewGuid();
        var clientIdB = Guid.NewGuid();

        var detailA = Serialize(new Method03Detail
        {
            FirstYearAmt = 1_000_000m,
            IncreaseRatePct = 0m,
            IncreaseRateYrs = 1
        });

        // Method-13 detail: clientId-only ref (no dbId — first save scenario)
        var detailB = Serialize(new Method13Detail
        {
            ProportionPct = 10m,
            RefTarget = new RefTarget
            {
                Kind = "assumption",
                ClientId = clientIdA.ToString(),
                DbId = null
            }
        });

        // Materialise domain entities and assign explicit IDs to avoid Guid.Empty collisions.
        var idMap = new Dictionary<Guid, Guid>();

        var section = IncomeSection.Create(analysis.Id, "income", "Income", "positive", 0);

        var category = IncomeCategory.Create(section.Id, "income", "Operating Income", "positive", 0);

        var assumptionA = IncomeAssumption.Create(category.Id, "I03", "Base Income", "positive", 0, "03", detailA);
        assumptionA.Id = dbIdA;   // explicit ID — mirrors EF NEWSEQUENTIALID() assignment
        idMap[clientIdA] = dbIdA;

        var assumptionB = IncomeAssumption.Create(category.Id, "M99", "Proportional", "positive", 1, "13", detailB);
        assumptionB.Id = dbIdB;
        idMap[clientIdB] = dbIdB;

        category.ReplaceAssumptions([assumptionA, assumptionB]);
        section.ReplaceCategories([category]);

        var summarySection = IncomeSection.Create(analysis.Id, "summaryDCF", "Summary", "empty", 1);

        analysis.ReplaceSections([section, summarySection]);

        return (analysis, idMap, clientIdA, dbIdA, dbIdB);
    }

    // ── Case 1: fresh tree — clientId only → dbId populated, M13 value non-zero ──

    [Fact]
    public void Rewrite_FreshTree_PopulatesDbIdFromClientId()
    {
        var (analysis, idMap, clientIdA, dbIdA, _) = BuildFreshSaveTree();

        var assumptionB = analysis.Sections
            .SelectMany(s => s.Categories)
            .SelectMany(c => c.Assumptions)
            .Single(a => a.Method.MethodTypeCode == "13");

        // Before rewrite: dbId is null
        var before = MethodDetailSerializer.Deserialize<Method13Detail>("13", assumptionB.Method.DetailJson);
        Assert.Null(before.RefTarget.DbId);
        Assert.Equal(clientIdA.ToString(), before.RefTarget.ClientId);

        // Act
        IncomeRefTargetRewriter.Rewrite(analysis, idMap, NullLogger.Instance);

        // After rewrite: dbId equals the runtime Guid assigned to assumption A
        var after = MethodDetailSerializer.Deserialize<Method13Detail>("13", assumptionB.Method.DetailJson);

        Assert.Equal(dbIdA.ToString(), after.RefTarget.DbId);
        // clientId is preserved (rewriter only sets dbId)
        Assert.Equal(clientIdA.ToString(), after.RefTarget.ClientId);
    }

    [Fact]
    public void Rewrite_FreshTree_AllowsCalculatorToResolveM13_ResultNonZero()
    {
        // End-to-end: after rewrite the calculator resolves the ref and B = 10% of A = 100_000.
        var (analysis, idMap, _, dbIdA, dbIdB) = BuildFreshSaveTree();

        IncomeRefTargetRewriter.Rewrite(analysis, idMap, NullLogger.Instance);

        var calcService = new IncomeCalculationService();
        var result = calcService.Calculate(analysis);

        Assert.Equal(1_000_000m, result.MethodValues[dbIdA][0]);
        Assert.Equal(100_000m, result.MethodValues[dbIdB][0]);
        Assert.Equal(100_000m, result.MethodValues[dbIdB][1]);
        Assert.Equal(100_000m, result.MethodValues[dbIdB][2]);
    }

    // ── Case 2: stale dbId is overwritten by the current save's idMap lookup ──

    [Fact]
    public void Rewrite_ResaveWithStaleDbId_OverwritesDbIdFromIdMap()
    {
        // Under full-replace semantics every Id rotates on each save, so a previously
        // stored dbId is stale.  The rewriter must overwrite it from the current idMap.
        var analysis = IncomeAnalysis.Create(
            Guid.NewGuid(), "dcf-test", "DCF Test", 2, 365, 5m, 0m);

        var clientIdA = Guid.NewGuid();
        var staleDbId = Guid.NewGuid();  // what the frontend sent back from last save
        var newDbId   = Guid.NewGuid();  // what the current save's idMap assigned

        var detailA = Serialize(new Method03Detail
        {
            FirstYearAmt = 500_000m,
            IncreaseRatePct = 0m,
            IncreaseRateYrs = 1
        });

        // Detail B: clientId present, dbId contains the stale previous-save value
        var detailB = Serialize(new Method13Detail
        {
            ProportionPct = 20m,
            RefTarget = new RefTarget
            {
                Kind = "assumption",
                ClientId = clientIdA.ToString(),
                DbId = staleDbId.ToString()
            }
        });

        var section = IncomeSection.Create(analysis.Id, "income", "Income", "positive", 0);
        var category = IncomeCategory.Create(section.Id, "income", "Cat", "positive", 0);
        var assumptionA = IncomeAssumption.Create(category.Id, "I03", "A", "positive", 0, "03", detailA);
        var assumptionB = IncomeAssumption.Create(category.Id, "M99", "B", "positive", 1, "13", detailB);
        category.ReplaceAssumptions([assumptionA, assumptionB]);
        section.ReplaceCategories([category]);
        var summarySection = IncomeSection.Create(analysis.Id, "summaryDCF", "Summary", "empty", 1);
        analysis.ReplaceSections([section, summarySection]);

        // idMap maps the same clientId to the freshly-assigned dbId for this save
        var idMap = new Dictionary<Guid, Guid>
        {
            [clientIdA] = newDbId
        };

        // Act
        IncomeRefTargetRewriter.Rewrite(analysis, idMap, NullLogger.Instance);

        var after = MethodDetailSerializer.Deserialize<Method13Detail>("13", assumptionB.Method.DetailJson);
        // Stale dbId must be replaced with the new one from the current save's idMap
        Assert.Equal(newDbId.ToString(), after.RefTarget.DbId);
        Assert.NotEqual(staleDbId.ToString(), after.RefTarget.DbId);
    }

    // ── Case 3: dangling clientId — save succeeds, dbId stays null, value = 0 ──

    [Fact]
    public void Rewrite_DanglingClientId_SaveSucceeds_DbIdRemainsNull_ValueZero()
    {
        var analysis = IncomeAnalysis.Create(
            Guid.NewGuid(), "dcf-test", "DCF Test", 2, 365, 5m, 0m);

        var danglingClientId = Guid.NewGuid();

        var detailB = Serialize(new Method13Detail
        {
            ProportionPct = 15m,
            RefTarget = new RefTarget
            {
                Kind = "assumption",
                ClientId = danglingClientId.ToString(),
                DbId = null
            }
        });

        var section = IncomeSection.Create(analysis.Id, "income", "Income", "positive", 0);
        var category = IncomeCategory.Create(section.Id, "income", "Cat", "positive", 0);
        var assumptionB = IncomeAssumption.Create(category.Id, "M99", "Dangling", "positive", 0, "13", detailB);
        assumptionB.Id = Guid.NewGuid();
        category.ReplaceAssumptions([assumptionB]);
        section.ReplaceCategories([category]);
        var summarySection = IncomeSection.Create(analysis.Id, "summaryDCF", "Summary", "empty", 1);
        analysis.ReplaceSections([section, summarySection]);

        // idMap is empty — dangling ref cannot be resolved
        var idMap = new Dictionary<Guid, Guid>();

        // Should not throw
        IncomeRefTargetRewriter.Rewrite(analysis, idMap, NullLogger.Instance);

        // dbId remains null
        var after = MethodDetailSerializer.Deserialize<Method13Detail>("13", assumptionB.Method.DetailJson);
        Assert.Null(after.RefTarget.DbId);

        // Calculator returns 0 for an unresolvable ref (not an exception)
        var calcService = new IncomeCalculationService();
        var result = calcService.Calculate(analysis);
        Assert.Equal(0m, result.MethodValues[assumptionB.Id][0]);
        Assert.Equal(0m, result.MethodValues[assumptionB.Id][1]);
    }

    // ── Case 5: dangling clientId with stale dbId — rewriter clears dbId ──

    [Fact]
    public void Rewrite_DanglingClientId_WithStaleDbId_ClearsDbId()
    {
        // clientId is present but not in the current idMap; a stale dbId from a previous save
        // would cause the calculator to silently return 0 (pointing at a deleted row).
        // The rewriter must clear it so the frontend shows "Please select" instead.
        var analysis = IncomeAnalysis.Create(
            Guid.NewGuid(), "dcf-test", "DCF Test", 2, 365, 5m, 0m);

        var danglingClientId = Guid.NewGuid();
        var staleDbId        = Guid.NewGuid();

        var detailB = Serialize(new Method13Detail
        {
            ProportionPct = 15m,
            RefTarget = new RefTarget
            {
                Kind     = "assumption",
                ClientId = danglingClientId.ToString(),
                DbId     = staleDbId.ToString()
            }
        });

        var section   = IncomeSection.Create(analysis.Id, "income", "Income", "positive", 0);
        var category  = IncomeCategory.Create(section.Id, "income", "Cat", "positive", 0);
        var assumptionB = IncomeAssumption.Create(category.Id, "M99", "Dangling", "positive", 0, "13", detailB);
        assumptionB.Id = Guid.NewGuid();
        category.ReplaceAssumptions([assumptionB]);
        section.ReplaceCategories([category]);
        var summarySection = IncomeSection.Create(analysis.Id, "summaryDCF", "Summary", "empty", 1);
        analysis.ReplaceSections([section, summarySection]);

        // idMap is empty — dangling ref cannot be resolved
        var idMap = new Dictionary<Guid, Guid>();

        // Should not throw
        IncomeRefTargetRewriter.Rewrite(analysis, idMap, NullLogger.Instance);

        // Stale dbId must be cleared — preserving it would point at a deleted row
        var after = MethodDetailSerializer.Deserialize<Method13Detail>("13", assumptionB.Method.DetailJson);
        Assert.Null(after.RefTarget.DbId);
    }

    // ── Case 4: idMap maps clientId to Guid.Empty — rewriter skips, dbId stays null ──

    [Fact]
    public void Rewrite_IdMapReturnsGuidEmpty_SkipsRewrite_DbIdRemainsNull()
    {
        // Simulates a defensive-recovery scenario: idMap contains the clientId key
        // but the value is Guid.Empty (which would have corrupted the JSON).
        // The new guard must skip the rewrite and leave dbId null.
        var analysis = IncomeAnalysis.Create(
            Guid.NewGuid(), "dcf-test", "DCF Test", 1, 365, 5m, 0m);

        var clientIdA = Guid.NewGuid();

        var detailM13 = Serialize(new Method13Detail
        {
            ProportionPct = 25m,
            RefTarget = new RefTarget
            {
                Kind = "assumption",
                ClientId = clientIdA.ToString(),
                DbId = null
            }
        });

        var section = IncomeSection.Create(analysis.Id, "income", "Income", "positive", 0);
        var category = IncomeCategory.Create(section.Id, "income", "Cat", "positive", 0);
        var assumptionM13 = IncomeAssumption.Create(category.Id, "M99", "M13 Node", "positive", 0, "13", detailM13);
        assumptionM13.Id = Guid.NewGuid();
        category.ReplaceAssumptions([assumptionM13]);
        section.ReplaceCategories([category]);
        var summarySection = IncomeSection.Create(analysis.Id, "summaryDCF", "Summary", "empty", 1);
        analysis.ReplaceSections([section, summarySection]);

        // idMap maps the clientId to Guid.Empty — the guard must block writing this into DetailJson
        var idMap = new Dictionary<Guid, Guid>
        {
            [clientIdA] = Guid.Empty
        };

        // Should not throw
        IncomeRefTargetRewriter.Rewrite(analysis, idMap, NullLogger.Instance);

        // dbId must remain null — writing "00000000-..." would silently corrupt M13 values
        var after = MethodDetailSerializer.Deserialize<Method13Detail>("13", assumptionM13.Method.DetailJson);
        Assert.Null(after.RefTarget.DbId);
    }
}
