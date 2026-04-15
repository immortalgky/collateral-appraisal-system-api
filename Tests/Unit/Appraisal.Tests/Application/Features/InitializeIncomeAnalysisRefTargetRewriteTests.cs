using System.Text.Json;
using Appraisal.Application.Features.PricingAnalysis.InitializeIncomeAnalysis;
using Appraisal.Domain.Appraisals.Income;
using Appraisal.Domain.Appraisals.Income.MethodDetails;
using Microsoft.Extensions.Logging.Abstractions;

namespace Appraisal.Tests.Application.Features;

/// <summary>
/// Tests for the method-13 refTarget.dbId rewrite performed during InitializeIncomeAnalysis.
///
/// When a template is cloned into a new IncomeAnalysis, every Section/Category/Assumption
/// gets a fresh runtime Guid.  Any method-13 detail whose refTarget.dbId points to a
/// template-world Guid must be rewritten to the corresponding runtime Guid; otherwise
/// IncomeCalculationService.ResolveRefTarget returns null and the proportional value is
/// always 0 for first-time analyses.
/// </summary>
public class InitializeIncomeAnalysisRefTargetRewriteTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string Serialize(object obj)
        => JsonSerializer.Serialize(obj, JsonOpts);

    // ── helpers to build a minimal fake "template" tree ──────────────────

    /// <summary>
    /// Simulates BuildSectionsFromTemplate from the handler:
    /// creates a 2-assumption section where assumption B method-13 references
    /// assumption A's template Guid via refTarget.dbId.
    /// Returns (templateAssumptionAId, sections, idMap).
    /// </summary>
    private static (Guid templateAId, List<IncomeSection> sections, Dictionary<Guid, Guid> idMap)
        BuildTemplateClone(Guid analysisId)
    {
        // Fake template-world GUIDs (mimic what would come from PricingTemplateAssumptionDto.Id)
        var templateSectionId = Guid.NewGuid();
        var templateCategoryId = Guid.NewGuid();
        var templateAssumptionAId = Guid.NewGuid();
        var templateAssumptionBId = Guid.NewGuid();

        // Detail for assumption A: simple step-growth, firstYearAmt=1_000_000
        var detailA = Serialize(new Method03Detail
        {
            FirstYearAmt = 1_000_000m,
            IncreaseRatePct = 0m,
            IncreaseRateYrs = 1
        });

        // Detail for assumption B: method-13 proportional of assumption A using template Guid
        var detailB = Serialize(new Method13Detail
        {
            ProportionPct = 10m,
            RefTarget = new RefTarget
            {
                Kind = "assumption",
                DbId = templateAssumptionAId.ToString()
            }
        });

        // Simulate what BuildSectionsFromTemplate produces: new runtime Guids, idMap populated
        var idMap = new Dictionary<Guid, Guid>();

        var section = IncomeSection.Create(analysisId, "income", "Income", "positive", 0);
        idMap[templateSectionId] = section.Id;

        var category = IncomeCategory.Create(section.Id, "income", "Operating Income", "positive", 0);
        idMap[templateCategoryId] = category.Id;

        var assumptionA = IncomeAssumption.Create(category.Id, "I03", "Base Income", "positive", 0, "03", detailA);
        idMap[templateAssumptionAId] = assumptionA.Id;  // <-- key mapping

        var assumptionB = IncomeAssumption.Create(category.Id, "M99", "Proportional", "positive", 1, "13", detailB);
        idMap[templateAssumptionBId] = assumptionB.Id;

        category.ReplaceAssumptions([assumptionA, assumptionB]);
        section.ReplaceCategories([category]);

        var summarySection = IncomeSection.Create(analysisId, "summaryDCF", "Summary", "empty", 1);

        return (templateAssumptionAId, [section, summarySection], idMap);
    }

    // ── BL-2 core: ref-target rewrite maps template GUID → runtime GUID ──

    [Fact]
    public void RewriteMethod13RefTargets_ReplacesTemplateGuidWithRuntimeGuid()
    {
        var analysis = IncomeAnalysis.Create(
            Guid.NewGuid(), "dcf-test", "DCF Test", 3, 365, 5m, 8m);

        var (templateAId, sections, idMap) = BuildTemplateClone(analysis.Id);
        analysis.ReplaceSections(sections);

        // The assumption B currently holds the template Guid in its DetailJson
        var assumptionB = analysis.Sections
            .SelectMany(s => s.Categories)
            .SelectMany(c => c.Assumptions)
            .Single(a => a.Method.MethodTypeCode == "13");

        var beforeDetail = MethodDetailSerializer.Deserialize<Method13Detail>("13", assumptionB.Method.DetailJson);
        Assert.Equal(templateAId.ToString(), beforeDetail.RefTarget.DbId);

        // Act
        InitializeIncomeAnalysisCommandHandler.RewriteMethod13RefTargets(
            analysis, idMap, NullLogger.Instance);

        // After rewrite: dbId must equal the runtime Guid for assumption A, not the template Guid
        var afterDetail = MethodDetailSerializer.Deserialize<Method13Detail>("13", assumptionB.Method.DetailJson);
        var runtimeAId = idMap[templateAId];

        Assert.Equal(runtimeAId.ToString(), afterDetail.RefTarget.DbId);
        Assert.NotEqual(templateAId.ToString(), afterDetail.RefTarget.DbId);
    }

    [Fact]
    public void RewriteMethod13RefTargets_RewrittenIdAllowsNonZeroCalculation()
    {
        // End-to-end: after rewrite, IncomeCalculationService resolves the ref
        // and assumption B gets a non-zero value (10% of assumption A = 100_000).

        var analysis = IncomeAnalysis.Create(
            Guid.NewGuid(), "dcf-test", "DCF Test", 3, 365, 5m, 8m);

        var (_, sections, idMap) = BuildTemplateClone(analysis.Id);
        analysis.ReplaceSections(sections);

        InitializeIncomeAnalysisCommandHandler.RewriteMethod13RefTargets(
            analysis, idMap, NullLogger.Instance);

        var calcService = new Appraisal.Domain.Services.IncomeCalculationService();
        var result = calcService.Calculate(analysis);

        // Assumption A: 1_000_000 per year
        var assumptionA = analysis.Sections
            .SelectMany(s => s.Categories)
            .SelectMany(c => c.Assumptions)
            .Single(a => a.Method.MethodTypeCode == "03");

        // Assumption B: 10% of A = 100_000 per year
        var assumptionB = analysis.Sections
            .SelectMany(s => s.Categories)
            .SelectMany(c => c.Assumptions)
            .Single(a => a.Method.MethodTypeCode == "13");

        Assert.Equal(1_000_000m, result.MethodValues[assumptionA.Id][0]);
        Assert.Equal(100_000m, result.MethodValues[assumptionB.Id][0]);
        Assert.Equal(100_000m, result.MethodValues[assumptionB.Id][1]);
        Assert.Equal(100_000m, result.MethodValues[assumptionB.Id][2]);
    }

    [Fact]
    public void RewriteMethod13RefTargets_LeavesOrphanRefUntouched_AndLogsWarning()
    {
        // An orphaned dbId (not in idMap) should be left as-is; no exception should be thrown.
        var analysis = IncomeAnalysis.Create(
            Guid.NewGuid(), "dcf-test", "DCF Test", 2, 365, 5m, 0m);

        var orphanGuid = Guid.NewGuid();
        var detailB = Serialize(new Method13Detail
        {
            ProportionPct = 5m,
            RefTarget = new RefTarget { Kind = "assumption", DbId = orphanGuid.ToString() }
        });

        var section = IncomeSection.Create(analysis.Id, "income", "Income", "positive", 0);
        var category = section.AddCategory("income", "Cat", "positive", 0);
        category.AddAssumption("M99", "Orphan ref", "positive", 0, "13", detailB);
        var summarySection = IncomeSection.Create(analysis.Id, "summaryDCF", "Summary", "empty", 1);
        analysis.ReplaceSections([section, summarySection]);

        var idMap = new Dictionary<Guid, Guid>(); // empty — no mapping for orphanGuid

        // Should not throw
        InitializeIncomeAnalysisCommandHandler.RewriteMethod13RefTargets(
            analysis, idMap, NullLogger.Instance);

        // DbId must remain the orphan Guid unchanged
        var assumption = analysis.Sections
            .SelectMany(s => s.Categories)
            .SelectMany(c => c.Assumptions)
            .Single();

        var detail = MethodDetailSerializer.Deserialize<Method13Detail>("13", assumption.Method.DetailJson);
        Assert.Equal(orphanGuid.ToString(), detail.RefTarget.DbId);
    }
}
