using System.Text.Json;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Income;
using Appraisal.Domain.Appraisals.Income.MethodDetails;
using Appraisal.Domain.Services;
using MediatR;
using Parameter.Contracts.PricingParameters;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.SaveIncomeAnalysis;

public class SaveIncomeAnalysisCommandHandler(
    IPricingAnalysisRepository repository,
    IncomeCalculationService calcService,
    ISender mediator
) : ICommandHandler<SaveIncomeAnalysisCommand, SaveIncomeAnalysisResult>
{

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task<SaveIncomeAnalysisResult> Handle(
        SaveIncomeAnalysisCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate
        var pricingAnalysis = await repository.GetByIdWithAllDataAsync(
                                 command.PricingAnalysisId, cancellationToken)
                             ?? throw new NotFoundException(
                                 nameof(Domain.Appraisals.PricingAnalysis),
                                 command.PricingAnalysisId);

        // 2. Find the target method and validate type
        var method = pricingAnalysis.Approaches
                         .SelectMany(a => a.Methods)
                         .FirstOrDefault(m => m.Id == command.MethodId)
                     ?? throw new NotFoundException(nameof(PricingAnalysisMethod), command.MethodId);

        if (!string.Equals(method.MethodType, "Income", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException(
                $"Method {command.MethodId} is not an Income method.");

        // 3. Validate all DetailJson before touching the aggregate
        ValidateDetailJsons(command.Sections);

        // 4. Rebuild IncomeAnalysis from scratch (full-replace semantics)
        var analysis = method.IncomeAnalysis;
        if (analysis is null)
        {
            analysis = IncomeAnalysis.Create(
                method.Id,
                command.TemplateCode,
                command.TemplateName,
                command.TotalNumberOfYears,
                command.TotalNumberOfDayInYear,
                command.CapitalizeRate,
                command.DiscountedRate);
            method.SetIncomeAnalysis(analysis);
        }
        else
        {
            analysis.UpdateParameters(
                command.TemplateCode,
                command.TemplateName,
                command.TotalNumberOfYears,
                command.TotalNumberOfDayInYear,
                command.CapitalizeRate,
                command.DiscountedRate);
        }

        // Build the new section tree
        var newSections = BuildSections(analysis.Id, command.Sections);
        analysis.ReplaceSections(newSections);

        // 5. Load tax brackets for Method-10 server-side derivation, then recalculate.
        var bracketsResult = await mediator.Send(new GetPricingTaxBracketsQuery(), cancellationToken);
        var result = calcService.Calculate(analysis, bracketsResult.Brackets);
        analysis.ApplyCalculationResult(result);

        // 6. Propagate final value to the method (always — zero is a valid result)
        method.SetValue(result.FinalValueRounded);

        // Propagate up the approach/analysis chain if this method is selected
        if (method.IsSelected)
        {
            var parentApproach = pricingAnalysis.Approaches
                .First(a => a.Methods.Any(m => m.Id == method.Id));
            parentApproach.SetValue(result.FinalValueRounded);

            if (parentApproach.IsSelected)
                pricingAnalysis.SetFinalValues(result.FinalValueRounded);
        }

        return new SaveIncomeAnalysisResult(IncomeAnalysisMapper.ToDto(analysis));
    }

    // ── Validation ────────────────────────────────────────────────────────

    private static void ValidateDetailJsons(IReadOnlyList<IncomeSectionInput> sections)
    {
        foreach (var section in sections)
        foreach (var category in section.Categories)
        foreach (var assumption in category.Assumptions)
        {
            var code = assumption.MethodTypeCode;
            var json = assumption.Detail.GetRawText();

            try
            {
                var parsed = MethodDetailSerializer.Deserialize(code, json);
                if (parsed is null)
                    throw new BadRequestException($"Unknown method type code '{code}'.");
            }
            catch (BadRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new BadRequestException(
                    $"Invalid detail for assumption '{assumption.AssumptionName}' (method code '{code}').",
                    ex.Message);
            }
        }
    }

    // ── Domain tree builder ───────────────────────────────────────────────

    private static List<IncomeSection> BuildSections(
        Guid analysisId,
        IReadOnlyList<IncomeSectionInput> inputs)
    {
        var sections = new List<IncomeSection>();
        foreach (var sInput in inputs)
        {
            var section = IncomeSection.Create(
                analysisId,
                sInput.SectionType,
                sInput.SectionName,
                sInput.Identifier,
                sInput.DisplaySeq);

            var categories = BuildCategories(section.Id, sInput.Categories);
            section.ReplaceCategories(categories);
            sections.Add(section);
        }
        return sections;
    }

    private static List<IncomeCategory> BuildCategories(
        Guid sectionId,
        IReadOnlyList<IncomeCategoryInput> inputs)
    {
        var categories = new List<IncomeCategory>();
        foreach (var cInput in inputs)
        {
            var category = IncomeCategory.Create(
                sectionId,
                cInput.CategoryType,
                cInput.CategoryName,
                cInput.Identifier,
                cInput.DisplaySeq);

            var assumptions = BuildAssumptions(category.Id, cInput.Assumptions);
            category.ReplaceAssumptions(assumptions);
            categories.Add(category);
        }
        return categories;
    }

    private static List<IncomeAssumption> BuildAssumptions(
        Guid categoryId,
        IReadOnlyList<IncomeAssumptionInput> inputs)
    {
        var assumptions = new List<IncomeAssumption>();
        foreach (var aInput in inputs)
        {
            var detailJson = aInput.Detail.GetRawText();
            var assumption = IncomeAssumption.Create(
                categoryId,
                aInput.AssumptionType,
                aInput.AssumptionName,
                aInput.Identifier,
                aInput.DisplaySeq,
                aInput.MethodTypeCode,
                detailJson);
            assumptions.Add(assumption);
        }
        return assumptions;
    }
}
