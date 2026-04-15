using System.Text.Json;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Income;
using Appraisal.Domain.Appraisals.Income.MethodDetails;
using Appraisal.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Parameter.Contracts.PricingParameters;
using Parameter.Contracts.PricingTemplates;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.InitializeIncomeAnalysis;

public class InitializeIncomeAnalysisCommandHandler(
    IPricingAnalysisRepository repository,
    ISender mediator,
    ILogger<InitializeIncomeAnalysisCommandHandler> logger,
    IncomeCalculationService calcService
) : ICommandHandler<InitializeIncomeAnalysisCommand, InitializeIncomeAnalysisResult>
{

    public async Task<InitializeIncomeAnalysisResult> Handle(
        InitializeIncomeAnalysisCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate
        var pricingAnalysis = await repository.GetByIdWithAllDataAsync(
                                 command.PricingAnalysisId, cancellationToken)
                             ?? throw new NotFoundException(
                                 nameof(Domain.Appraisals.PricingAnalysis),
                                 command.PricingAnalysisId);

        // 2. Find method and validate type
        var method = pricingAnalysis.Approaches
                         .SelectMany(a => a.Methods)
                         .FirstOrDefault(m => m.Id == command.MethodId)
                     ?? throw new NotFoundException(nameof(PricingAnalysisMethod), command.MethodId);

        if (!string.Equals(method.MethodType, "Income", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Method {command.MethodId} has type '{method.MethodType}', expected 'Income'.");

        // 3. Guard against overwrite — caller must use SaveIncomeAnalysis to replace an existing analysis
        if (method.IncomeAnalysis is not null)
            throw new ConflictException(
                $"An IncomeAnalysis already exists for method {command.MethodId}. " +
                "Use PUT income-analysis to replace it.");

        // 4. Fetch the template blueprint via cross-module MediatR call
        var templateResult = await mediator.Send(
            new GetPricingTemplateQuery(command.TemplateCode), cancellationToken);

        var template = templateResult.Template;

        // 5. Build a new IncomeAnalysis cloning the template
        //    Fresh Guids everywhere — never reuse template IDs.
        //    We also accumulate a templateId → runtimeId map so method-13 refTarget.dbId
        //    can be rewritten from template-world GUIDs to new runtime GUIDs.
        var analysis = IncomeAnalysis.Create(
            pricingAnalysisMethodId: method.Id,
            templateCode: template.Code,
            templateName: template.Name,
            totalNumberOfYears: template.TotalNumberOfYears,
            totalNumberOfDayInYear: template.TotalNumberOfDayInYear,
            capitalizeRate: template.CapitalizeRate,
            discountedRate: template.DiscountedRate);

        var idMap = new Dictionary<Guid, Guid>();
        var sections = BuildSectionsFromTemplate(analysis.Id, template.Sections, idMap);
        analysis.ReplaceSections(sections);

        // 6. Rewrite method-13 refTarget.dbId values — template GUIDs → runtime GUIDs
        RewriteMethod13RefTargets(analysis, idMap, logger);

        method.SetIncomeAnalysis(analysis);

        // 7. Load tax brackets for Method-10 server-side derivation, then run initial calculation.
        var bracketsResult = await mediator.Send(new GetPricingTaxBracketsQuery(), cancellationToken);
        var calcResult = calcService.Calculate(analysis, bracketsResult.Brackets);
        analysis.ApplyCalculationResult(calcResult);

        if (calcResult.FinalValueRounded != 0)
            method.SetValue(calcResult.FinalValueRounded);

        return new InitializeIncomeAnalysisResult(IncomeAnalysisMapper.ToDto(analysis));
    }

    // ── Template → domain tree builder ───────────────────────────────────

    private static List<IncomeSection> BuildSectionsFromTemplate(
        Guid analysisId,
        IEnumerable<PricingTemplateSectionDto> templateSections,
        Dictionary<Guid, Guid> idMap)
    {
        var sections = new List<IncomeSection>();
        foreach (var ts in templateSections)
        {
            var section = IncomeSection.Create(
                analysisId,
                ts.SectionType,
                ts.SectionName,
                ts.Identifier,
                ts.DisplaySeq);

            idMap[ts.Id] = section.Id;

            var categories = BuildCategoriesFromTemplate(section.Id, ts.Categories, idMap);
            section.ReplaceCategories(categories);
            sections.Add(section);
        }
        return sections;
    }

    private static List<IncomeCategory> BuildCategoriesFromTemplate(
        Guid sectionId,
        IEnumerable<PricingTemplateCategoryDto> templateCategories,
        Dictionary<Guid, Guid> idMap)
    {
        var categories = new List<IncomeCategory>();
        foreach (var tc in templateCategories)
        {
            var category = IncomeCategory.Create(
                sectionId,
                tc.CategoryType,
                tc.CategoryName,
                tc.Identifier,
                tc.DisplaySeq);

            idMap[tc.Id] = category.Id;

            var assumptions = BuildAssumptionsFromTemplate(category.Id, tc.Assumptions, idMap);
            category.ReplaceAssumptions(assumptions);
            categories.Add(category);
        }
        return categories;
    }

    private static List<IncomeAssumption> BuildAssumptionsFromTemplate(
        Guid categoryId,
        IEnumerable<PricingTemplateAssumptionDto> templateAssumptions,
        Dictionary<Guid, Guid> idMap)
    {
        var assumptions = new List<IncomeAssumption>();
        foreach (var ta in templateAssumptions)
        {
            // MethodDetailJson is copied verbatim from the template;
            // method-13 refTarget.dbId values are rewritten in a second pass below.
            var assumption = IncomeAssumption.Create(
                categoryId,
                ta.AssumptionType,
                ta.AssumptionName,
                ta.Identifier,
                ta.DisplaySeq,
                ta.MethodTypeCode,
                ta.MethodDetailJson);

            idMap[ta.Id] = assumption.Id;
            assumptions.Add(assumption);
        }
        return assumptions;
    }

    // ── Method-13 ref-target rewrite ──────────────────────────────────────

    /// <summary>
    /// Walks every method-13 assumption and replaces its refTarget.dbId
    /// (which currently holds a template-world GUID) with the corresponding
    /// runtime GUID minted during cloning.
    /// Called after <see cref="BuildSectionsFromTemplate"/> has populated <paramref name="idMap"/>.
    /// </summary>
    public static void RewriteMethod13RefTargets(
        IncomeAnalysis analysis,
        Dictionary<Guid, Guid> idMap,
        ILogger logger)
    {
        foreach (var section in analysis.Sections)
        foreach (var category in section.Categories)
        foreach (var assumption in category.Assumptions)
        {
            if (assumption.Method.MethodTypeCode != "13")
                continue;

            var detail = MethodDetailSerializer.Deserialize<Method13Detail>(
                "13", assumption.Method.DetailJson);

            var dbIdStr = detail.RefTarget.DbId;
            if (string.IsNullOrWhiteSpace(dbIdStr))
                continue;

            if (!Guid.TryParse(dbIdStr, out var templateGuid))
                continue;

            if (!idMap.TryGetValue(templateGuid, out var runtimeGuid))
            {
                logger.LogWarning(
                    "Method-13 refTarget.dbId {TemplateGuid} for assumption {AssumptionId} " +
                    "was not found in the template ID map — leaving as-is.",
                    templateGuid, assumption.Id);
                continue;
            }

            var rewritten = detail with
            {
                RefTarget = detail.RefTarget with { DbId = runtimeGuid.ToString() }
            };

            assumption.Method.SetDetailJson(MethodDetailSerializer.Serialize(rewritten));
        }
    }
}
