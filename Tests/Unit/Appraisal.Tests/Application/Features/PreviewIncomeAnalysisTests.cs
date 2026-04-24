using System.Text.Json;
using Appraisal.Application.Features.PricingAnalysis.PreviewIncomeAnalysis;
using Appraisal.Application.Features.PricingAnalysis.SaveIncomeAnalysis;
using Appraisal.Contracts.Appraisals.Dto.Income;
using Appraisal.Domain.Appraisals.Income;
using Appraisal.Domain.Appraisals.Income.MethodDetails;
using Appraisal.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parameter.Contracts.PricingParameters;

namespace Appraisal.Tests.Application.Features;

/// <summary>
/// Tests for the no-persistence preview endpoint handler.
///
/// Key contract: PreviewIncomeAnalysisCommandHandler runs the exact same
/// calculation as Save but never calls SaveChangesAsync — the IncomeAnalysis
/// object is a transient in-memory graph that is garbage-collected on return.
/// </summary>
public class PreviewIncomeAnalysisTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string Serialize(object obj) => JsonSerializer.Serialize(obj, JsonOpts);

    // ── Minimal ISender stub — returns an empty tax-brackets result ─────────

    /// <summary>
    /// Minimal ISender that returns an empty <see cref="GetPricingTaxBracketsResult"/>
    /// so tests don't need a real MediatR pipeline.
    /// </summary>
    private sealed class StubSender(IReadOnlyList<TaxBracketDto>? brackets = null) : ISender
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is GetPricingTaxBracketsQuery)
            {
                var result = new GetPricingTaxBracketsResult(brackets ?? []);
                return Task.FromResult((TResponse)(object)result);
            }
            throw new InvalidOperationException($"StubSender: unexpected request type {request.GetType().Name}");
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
            => throw new NotImplementedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    // ── Helper: build a command with M03 + M13 assumptions ─────────────────

    /// <summary>
    /// Builds a 3-year DCF tree with:
    ///   Section "income"
    ///     Category "Operating Income"
    ///       A — M03 (fixed income 1_000_000/yr)
    ///       B — M13 (10% of A)  ← proportional ref
    /// </summary>
    private static (PreviewIncomeAnalysisCommand command, Guid clientIdA, Guid clientIdB)
        BuildCommandWithM03AndM13()
    {
        var clientIdA = Guid.NewGuid();
        var clientIdB = Guid.NewGuid();

        var detailA = Serialize(new Method03Detail
        {
            FirstYearAmt = 1_000_000m,
            IncreaseRatePct = 0m,
            IncreaseRateYrs = 1
        });

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

        var sections = new List<IncomeSectionInput>
        {
            new IncomeSectionInput(
                SectionType: "income",
                SectionName: "Income",
                Identifier: "positive",
                DisplaySeq: 0,
                Categories: new List<IncomeCategoryInput>
                {
                    new IncomeCategoryInput(
                        CategoryType: "income",
                        CategoryName: "Operating Income",
                        Identifier: "positive",
                        DisplaySeq: 0,
                        Assumptions: new List<IncomeAssumptionInput>
                        {
                            new IncomeAssumptionInput(
                                AssumptionType: "I03",
                                AssumptionName: "Base Income",
                                Identifier: "positive",
                                DisplaySeq: 0,
                                MethodTypeCode: "03",
                                Detail: JsonDocument.Parse(detailA).RootElement,
                                ClientId: clientIdA.ToString()),
                            new IncomeAssumptionInput(
                                AssumptionType: "M99",
                                AssumptionName: "Admin Fee",
                                Identifier: "positive",
                                DisplaySeq: 1,
                                MethodTypeCode: "13",
                                Detail: JsonDocument.Parse(detailB).RootElement,
                                ClientId: clientIdB.ToString())
                        },
                        ClientId: Guid.NewGuid().ToString())
                },
                ClientId: Guid.NewGuid().ToString()),
            new IncomeSectionInput(
                SectionType: "summaryDCF",
                SectionName: "Summary",
                Identifier: "empty",
                DisplaySeq: 1,
                Categories: new List<IncomeCategoryInput>(),
                ClientId: Guid.NewGuid().ToString())
        };

        var command = new PreviewIncomeAnalysisCommand(
            PricingAnalysisId: Guid.NewGuid(),
            MethodId: Guid.NewGuid(),
            AppraisalId: Guid.NewGuid(),
            PropertyId: Guid.NewGuid(),
            TemplateCode: "dcf-hotel",
            TemplateName: "DCF Hotel",
            TotalNumberOfYears: 3,
            TotalNumberOfDayInYear: 365,
            CapitalizeRate: 5m,
            DiscountedRate: 8m,
            Sections: sections);

        return (command, clientIdA, clientIdB);
    }

    // ── Test 1: Happy path — non-zero FinalValueRounded, M13 proportional correct ──

    [Fact]
    public async Task Handle_HappyPath_ReturnsNonZeroFinalValue_AndM13ProportionalCorrect()
    {
        var (command, _, _) = BuildCommandWithM03AndM13();
        var handler = new PreviewIncomeAnalysisCommandHandler(
            new IncomeCalculationService(),
            new StubSender(),
            NullLogger<PreviewIncomeAnalysisCommandHandler>.Instance);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result.Analysis);
        Assert.NotNull(result.Analysis.FinalValueRounded);
        Assert.True(result.Analysis.FinalValueRounded > 0,
            $"FinalValueRounded should be > 0 but was {result.Analysis.FinalValueRounded}");

        // M13 (Admin Fee) = 10% of M03 (Base Income) per year
        // M03 is 1_000_000/yr flat; M13 must equal 100_000/yr
        var section = result.Analysis.Sections.First(s => s.SectionType == "income");
        var category = section.Categories.First();
        var m03 = category.Assumptions.First(a => a.Method.MethodTypeCode == "03");
        var m13 = category.Assumptions.First(a => a.Method.MethodTypeCode == "13");

        Assert.NotEmpty(m03.Method.TotalMethodValues);
        Assert.NotEmpty(m13.Method.TotalMethodValues);

        for (var y = 0; y < m03.Method.TotalMethodValues.Length; y++)
        {
            var expected = m03.Method.TotalMethodValues[y] * 0.10m;
            Assert.Equal(expected, m13.Method.TotalMethodValues[y], precision: 2);
        }
    }

    // ── Test 2: No persistence — UoW is never consulted ────────────────────

    [Fact]
    public async Task Handle_NoPersistence_HandlerDoesNotResolveUoW()
    {
        // The handler constructor takes only (IncomeCalculationService, ISender, ILogger).
        // If it ever tried to inject IAppraisalUnitOfWork, construction would fail here.
        // This test documents and enforces the no-persistence contract.
        var (command, _, _) = BuildCommandWithM03AndM13();
        var handler = new PreviewIncomeAnalysisCommandHandler(
            new IncomeCalculationService(),
            new StubSender(),
            NullLogger<PreviewIncomeAnalysisCommandHandler>.Instance);

        // Should complete without any DB interaction
        var result = await handler.Handle(command, CancellationToken.None);
        Assert.NotNull(result);
    }

    // ── Test 3: Parity with direct calc — preview matches manual calculation ─

    [Fact]
    public async Task Handle_OutputMatchesDirectCalcResult()
    {
        // Build the same in-memory tree manually and run IncomeCalculationService directly,
        // then compare against the handler's result to confirm identical numbers.
        var (command, clientIdA, _) = BuildCommandWithM03AndM13();

        var handler = new PreviewIncomeAnalysisCommandHandler(
            new IncomeCalculationService(),
            new StubSender(),
            NullLogger<PreviewIncomeAnalysisCommandHandler>.Instance);

        var handlerResult = await handler.Handle(command, CancellationToken.None);

        // Build a parallel domain tree and run calc directly
        var analysis = IncomeAnalysis.Create(Guid.NewGuid(), "dcf-hotel", "DCF Hotel", 3, 365, 5m, 8m, id: Guid.NewGuid());

        var section = IncomeSection.Create(analysis.Id, "income", "Income", "positive", 0, id: Guid.NewGuid());
        var category = IncomeCategory.Create(section.Id, "income", "Operating Income", "positive", 0, id: Guid.NewGuid());

        var m03Input = command.Sections[0].Categories[0].Assumptions[0];
        var m13Input = command.Sections[0].Categories[0].Assumptions[1];

        var assumptionA = IncomeAssumption.Create(category.Id, "I03", "Base Income", "positive", 0, "03", m03Input.Detail.GetRawText(), id: Guid.NewGuid());
        var assumptionB = IncomeAssumption.Create(category.Id, "M99", "Admin Fee", "positive", 1, "13", m13Input.Detail.GetRawText(), id: Guid.NewGuid());

        var idMap = new Dictionary<Guid, Guid>
        {
            [clientIdA] = assumptionA.Id
        };

        category.ReplaceAssumptions([assumptionA, assumptionB]);
        section.ReplaceCategories([category]);
        var summarySection = IncomeSection.Create(analysis.Id, "summaryDCF", "Summary", "empty", 1, id: Guid.NewGuid());
        analysis.ReplaceSections([section, summarySection]);

        IncomeRefTargetRewriter.Rewrite(analysis, idMap, NullLogger.Instance);

        var calcResult = new IncomeCalculationService().Calculate(analysis);
        analysis.ApplyCalculationResult(calcResult);

        Assert.Equal(analysis.FinalValueRounded, handlerResult.Analysis.FinalValueRounded);
    }

    // ── Test 5: M13 iteration convergence — Fixed Charge = 5% of GOP (which itself contains M13 expenses) ──

    /// <summary>
    /// Guards against regression if the iteration loop is removed or capped at 1 pass.
    ///
    /// Tree (1 year):
    ///   Income section (positive)
    ///     Operating Income (income): M01 → Room Rental = 36,500
    ///   Expenses section (negative)
    ///     Direct Operating Expenses (expenses): M13 → 10% of Room Rental assumption = 3,650
    ///     Admin Mgmt Expenses (expenses):       M13 → 20% of Room Rental assumption = 7,300
    ///     GOP (gop):                            computed = 36,500 − 3,650 − 7,300 = 25,550
    ///     Fixed Charge (fixedExps):             M13 → 5% of GOP category = 1,277.5
    ///
    /// Single-pass (no iteration) would compute Fixed Charge as 5% × 36,500 = 1,825
    /// because on pass 1 the GOP aggregate has not yet incorporated M13 expenses.
    /// After convergence (≥ 2 iterations) the correct value is 1,277.5.
    /// </summary>
    [Fact]
    public void Calculate_M13IterationConvergence_FixedChargeUsesConvergedGop()
    {
        var sut = new IncomeCalculationService();

        // Pre-assign Guids so M13 refTarget.dbId can be set directly (no clientId rewrite needed).
        var analysisId = Guid.NewGuid();

        // Income section entities
        var incomeSectionId = Guid.NewGuid();
        var incomeCategId = Guid.NewGuid();
        var m01AssumptionId = Guid.NewGuid();

        // Expenses section entities
        var expensesSectionId = Guid.NewGuid();
        var doeCategId = Guid.NewGuid();
        var doeAssumptionId = Guid.NewGuid();
        var ameCategId = Guid.NewGuid();
        var ameAssumptionId = Guid.NewGuid();
        var gopCategId = Guid.NewGuid();
        var fixedCategId = Guid.NewGuid();
        var fixedAssumptionId = Guid.NewGuid();

        // Build the analysis
        var analysis = IncomeAnalysis.Create(
            pricingAnalysisMethodId: Guid.NewGuid(),
            templateCode: "dcf-test",
            templateName: "Convergence Test",
            totalNumberOfYears: 1,
            totalNumberOfDayInYear: 365,
            capitalizeRate: 0m,
            discountedRate: 0m,
            id: analysisId);

        // M01 detail: sumSaleableArea=10, avgRoomRate=10, occupancy=100%, 1 year
        // Room Rental y0 = 10 × 365 × (100/100) × 10 = 36,500
        var m01Detail = Serialize(new Method01Detail
        {
            SumSaleableArea = 10m,
            AvgRoomRate = 10m,
            IncreaseRatePct = 0m,
            IncreaseRateYrs = 1,
            OccupancyRateFirstYearPct = 100m,
            OccupancyRatePct = 0m,
            OccupancyRateYrs = 1
        });

        // Income section
        var incomeSection = IncomeSection.Create(analysisId, "income", "Income", "positive", 0, id: incomeSectionId);
        var incomeCategory = IncomeCategory.Create(incomeSectionId, "income", "Operating Income", "positive", 0, id: incomeCategId);
        var m01Assumption = IncomeAssumption.Create(incomeCategId, "I01", "Room Rental", "positive", 0, "01", m01Detail, id: m01AssumptionId);
        incomeCategory.ReplaceAssumptions([m01Assumption]);
        incomeSection.ReplaceCategories([incomeCategory]);

        // Expenses section: DOE (10% of M01), AME (20% of M01), GOP (computed), Fixed (5% of GOP)
        var expensesSection = IncomeSection.Create(analysisId, "expenses", "Expenses", "negative", 1, id: expensesSectionId);

        // DOE: 10% of the M01 assumption
        var doeCategory = IncomeCategory.Create(expensesSectionId, "expenses", "Direct Operating Expenses", "negative", 0, id: doeCategId);
        var doeDetail = Serialize(new Method13Detail
        {
            ProportionPct = 10m,
            RefTarget = new RefTarget { Kind = "assumption", DbId = m01AssumptionId.ToString() }
        });
        var doeAssumption = IncomeAssumption.Create(doeCategId, "E01", "DOE", "negative", 0, "13", doeDetail, id: doeAssumptionId);
        doeCategory.ReplaceAssumptions([doeAssumption]);

        // AME: 20% of the M01 assumption
        var ameCategory = IncomeCategory.Create(expensesSectionId, "expenses", "Admin Mgmt Expenses", "negative", 1, id: ameCategId);
        var ameDetail = Serialize(new Method13Detail
        {
            ProportionPct = 20m,
            RefTarget = new RefTarget { Kind = "assumption", DbId = m01AssumptionId.ToString() }
        });
        var ameAssumption = IncomeAssumption.Create(ameCategId, "E02", "AME", "negative", 1, "13", ameDetail, id: ameAssumptionId);
        ameCategory.ReplaceAssumptions([ameAssumption]);

        // GOP category (no assumptions — value is derived by aggregation)
        var gopCategory = IncomeCategory.Create(expensesSectionId, "gop", "GOP", "empty", 2, id: gopCategId);
        gopCategory.ReplaceAssumptions([]);

        // Fixed Charge: 5% of GOP category
        var fixedCategory = IncomeCategory.Create(expensesSectionId, "fixedExps", "Fixed Charge", "negative", 3, id: fixedCategId);
        var fixedDetail = Serialize(new Method13Detail
        {
            ProportionPct = 5m,
            RefTarget = new RefTarget { Kind = "category", DbId = gopCategId.ToString() }
        });
        var fixedAssumption = IncomeAssumption.Create(fixedCategId, "E03", "Fixed Charge", "negative", 0, "13", fixedDetail, id: fixedAssumptionId);
        fixedCategory.ReplaceAssumptions([fixedAssumption]);

        expensesSection.ReplaceCategories([doeCategory, ameCategory, gopCategory, fixedCategory]);

        // summaryDCF section (required by pipeline)
        var summarySection = IncomeSection.Create(analysisId, "summaryDCF", "Summary", "empty", 2, id: Guid.NewGuid());

        analysis.ReplaceSections([incomeSection, expensesSection, summarySection]);

        // Act
        var result = sut.Calculate(analysis, null);

        // Assert — convergence values (not single-pass values)
        // Room Rental = 10 × 365 × 1.00 × 10 = 36,500
        Assert.Equal(36_500m, result.MethodValues[m01AssumptionId][0]);

        // DOE = 10% × 36,500 = 3,650
        Assert.Equal(3_650m, result.AssumptionValues[doeAssumptionId][0]);

        // AME = 20% × 36,500 = 7,300
        Assert.Equal(7_300m, result.AssumptionValues[ameAssumptionId][0]);

        // GOP category = 36,500 − 3,650 − 7,300 = 25,550 (post-convergence)
        Assert.Equal(25_550m, result.CategoryValues[gopCategId][0]);

        // Fixed Charge = 5% × 25,550 = 1,277.5  (converged)
        // Single-pass (wrong) value would be 5% × 36,500 = 1,825
        Assert.Equal(1_277.5m, result.AssumptionValues[fixedAssumptionId][0]);
    }

    // ── Spy logger — captures Warning-level messages for assertion ─────────────

    private sealed class SpyLogger<T> : ILogger<T>
    {
        private readonly List<string> _warnings = [];
        public IReadOnlyList<string> Warnings => _warnings;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning)
                _warnings.Add(formatter(state, exception));
        }
    }

    // ── Test S1: Non-convergence warning — circular M13 refs that diverge across iterations ──

    /// <summary>
    /// A non-converging M13 graph built so that values grow each iteration and never stabilise
    /// within MaxIterations=5 passes.
    ///
    /// Tree (1 year):
    ///   income section (positive)
    ///     Category X (income): M03 seed=1_000, B = 100% of Category Y
    ///     Category Y (income): A = 100% of Category X
    ///
    /// Iteration trace (catX total = M03 + B, catY total = A):
    ///   iter 0: A = catX(1000) = 1000, B = catY(0)  = 0    → changed
    ///   iter 1: A = catX(1000) = 1000, B = catY(1000)= 1000 → changed
    ///   iter 2: A = catX(2000) = 2000, B = catY(1000)= 1000 → changed
    ///   iter 3: A = catX(2000) = 2000, B = catY(2000)= 2000 → changed
    ///   iter 4: A = catX(3000) = 3000, B = catY(2000)= 2000 → changed → didNotConverge
    /// </summary>
    [Fact]
    public void Calculate_M13NonConvergence_LogsWarningAndTerminates()
    {
        var spyLogger = new SpyLogger<IncomeCalculationService>();
        var sut = new IncomeCalculationService(spyLogger);

        var analysisId = Guid.NewGuid();
        var catXId     = Guid.NewGuid();
        var catYId     = Guid.NewGuid();
        var m03Id      = Guid.NewGuid();
        var asmAId     = Guid.NewGuid();
        var asmBId     = Guid.NewGuid();

        var analysis = IncomeAnalysis.Create(
            pricingAnalysisMethodId: Guid.NewGuid(),
            templateCode: "dcf-test",
            templateName: "NonConvergence Test",
            totalNumberOfYears: 1,
            totalNumberOfDayInYear: 365,
            capitalizeRate: 0m,
            discountedRate: 0m,
            id: analysisId);

        var section = IncomeSection.Create(analysisId, "income", "Income", "positive", 0, id: Guid.NewGuid());

        // Category X: M03 (seed 1_000/yr) + B (100% of Category Y)
        var m03Detail = Serialize(new Method03Detail { FirstYearAmt = 1_000m, IncreaseRatePct = 0m, IncreaseRateYrs = 1 });
        var detailB = Serialize(new Method13Detail
        {
            ProportionPct = 100m,
            RefTarget = new RefTarget { Kind = "category", DbId = catYId.ToString() }
        });
        var catX  = IncomeCategory.Create(section.Id, "income", "Cat X", "positive", 0, id: catXId);
        var m03   = IncomeAssumption.Create(catXId, "I03", "Seed", "positive", 0, "03", m03Detail, id: m03Id);
        var asmB  = IncomeAssumption.Create(catXId, "M13", "B", "positive", 1, "13", detailB, id: asmBId);
        catX.ReplaceAssumptions([m03, asmB]);

        // Category Y: A = 100% of Category X
        var detailA = Serialize(new Method13Detail
        {
            ProportionPct = 100m,
            RefTarget = new RefTarget { Kind = "category", DbId = catXId.ToString() }
        });
        var catY  = IncomeCategory.Create(section.Id, "income", "Cat Y", "positive", 1, id: catYId);
        var asmA  = IncomeAssumption.Create(catYId, "M13", "A", "positive", 0, "13", detailA, id: asmAId);
        catY.ReplaceAssumptions([asmA]);

        section.ReplaceCategories([catX, catY]);

        var summarySection = IncomeSection.Create(analysisId, "summaryDCF", "Summary", "empty", 1, id: Guid.NewGuid());
        analysis.ReplaceSections([section, summarySection]);

        // Act — must terminate without throwing
        var result = sut.Calculate(analysis, null);

        // Loop terminated (no StackOverflow / infinite hang)
        Assert.NotNull(result);

        // Warning was emitted
        Assert.Contains(spyLogger.Warnings, w => w.Contains("did not converge"));
    }

    // ── Test 4: Dangling M13 ref — rewriter nulls dbId, calc returns 0, no exception ──

    [Fact]
    public async Task Handle_DanglingM13Ref_ReturnsZeroForThatAssumption_NoException()
    {
        var danglingClientId = Guid.NewGuid(); // Not referenced by any assumption in the tree

        var detailBase = Serialize(new Method03Detail
        {
            FirstYearAmt = 500_000m,
            IncreaseRatePct = 0m,
            IncreaseRateYrs = 1
        });

        var detailDangling = Serialize(new Method13Detail
        {
            ProportionPct = 20m,
            RefTarget = new RefTarget
            {
                Kind = "assumption",
                ClientId = danglingClientId.ToString(), // points at nobody
                DbId = null
            }
        });

        var sections = new List<IncomeSectionInput>
        {
            new IncomeSectionInput(
                SectionType: "income",
                SectionName: "Income",
                Identifier: "positive",
                DisplaySeq: 0,
                Categories: new List<IncomeCategoryInput>
                {
                    new IncomeCategoryInput(
                        CategoryType: "income",
                        CategoryName: "Cat",
                        Identifier: "positive",
                        DisplaySeq: 0,
                        Assumptions: new List<IncomeAssumptionInput>
                        {
                            new IncomeAssumptionInput(
                                AssumptionType: "I03",
                                AssumptionName: "Base",
                                Identifier: "positive",
                                DisplaySeq: 0,
                                MethodTypeCode: "03",
                                Detail: JsonDocument.Parse(detailBase).RootElement,
                                ClientId: Guid.NewGuid().ToString()),
                            new IncomeAssumptionInput(
                                AssumptionType: "M99",
                                AssumptionName: "Dangling Fee",
                                Identifier: "positive",
                                DisplaySeq: 1,
                                MethodTypeCode: "13",
                                Detail: JsonDocument.Parse(detailDangling).RootElement,
                                ClientId: Guid.NewGuid().ToString())
                        },
                        ClientId: Guid.NewGuid().ToString())
                },
                ClientId: Guid.NewGuid().ToString()),
            new IncomeSectionInput(
                SectionType: "summaryDCF",
                SectionName: "Summary",
                Identifier: "empty",
                DisplaySeq: 1,
                Categories: new List<IncomeCategoryInput>(),
                ClientId: Guid.NewGuid().ToString())
        };

        var command = new PreviewIncomeAnalysisCommand(
            Guid.NewGuid(), Guid.NewGuid(),Guid.NewGuid(), Guid.NewGuid(),
            "dcf-test", "DCF Test",
            TotalNumberOfYears: 2,
            TotalNumberOfDayInYear: 365,
            CapitalizeRate: 5m,
            DiscountedRate: 0m,
            Sections: sections);

        var handler = new PreviewIncomeAnalysisCommandHandler(
            new IncomeCalculationService(),
            new StubSender(),
            NullLogger<PreviewIncomeAnalysisCommandHandler>.Instance);

        // Should not throw — dangling refs are handled gracefully
        var result = await handler.Handle(command, CancellationToken.None);

        var section = result.Analysis.Sections.First(s => s.SectionType == "income");
        var danglingAssumption = section.Categories.First()
            .Assumptions.First(a => a.Method.MethodTypeCode == "13");

        // dbId in the returned JSON should be null (rewriter clears it)
        var detail = JsonSerializer.Deserialize<Method13Detail>(
            danglingAssumption.Method.Detail.GetRawText(), JsonOpts);
        Assert.Null(detail!.RefTarget.DbId);

        // All method values for the dangling M13 should be 0
        Assert.All(danglingAssumption.Method.TotalMethodValues, v => Assert.Equal(0m, v));
    }
}
