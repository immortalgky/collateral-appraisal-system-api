using Appraisal.Application.Features.PricingAnalysis.SaveIncomeAnalysis;
using Appraisal.Domain.Appraisals.Income;
using Appraisal.Domain.Appraisals.Income.MethodDetails;
using Appraisal.Domain.Services;
using Parameter.Contracts.PricingParameters;

namespace Appraisal.Application.Features.PricingAnalysis.PreviewIncomeAnalysis;

public class PreviewIncomeAnalysisCommandHandler(
    IncomeCalculationService calcService,
    ISender mediator,
    ILogger<PreviewIncomeAnalysisCommandHandler> logger
) : ICommandHandler<PreviewIncomeAnalysisCommand, PreviewIncomeAnalysisResult>
{
    public async Task<PreviewIncomeAnalysisResult> Handle(
        PreviewIncomeAnalysisCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Validate all DetailJsons upfront — same rules as Save
        ValidateDetailJsons(command.Sections);

        // 2. Build a transient in-memory IncomeAnalysis — never attached to any DbContext
        var analysis = IncomeAnalysis.Create(
            pricingAnalysisMethodId: Guid.NewGuid(), // dummy FK — not persisted, not used in calc
            command.TemplateCode,
            command.TemplateName,
            command.TotalNumberOfYears,
            command.TotalNumberOfDayInYear,
            command.CapitalizeRate,
            command.DiscountedRate,
            id: Guid.NewGuid());

        // 3. Build section tree — every entity gets a real Guid so the calc service can key on Id
        var newSections = BuildSectionsWithIds(analysis.Id, command.Sections);
        analysis.ReplaceSections(newSections);

        // 4. Build clientId → in-memory-dbId map (no flush needed — Ids were assigned in step 3)
        var idMap = new Dictionary<Guid, Guid>();
        foreach (var (sInput, section) in command.Sections.Zip(analysis.Sections))
        {
            if (Guid.TryParse(sInput.ClientId, out var sCid)) idMap[sCid] = section.Id;
            foreach (var (cInput, category) in sInput.Categories.Zip(section.Categories))
            {
                if (Guid.TryParse(cInput.ClientId, out var cCid)) idMap[cCid] = category.Id;
                foreach (var (aInput, assumption) in cInput.Assumptions.Zip(category.Assumptions))
                    if (Guid.TryParse(aInput.ClientId, out var aCid)) idMap[aCid] = assumption.Id;
            }
        }

        // 5. Rewrite Method-13 refTarget.clientId → in-memory dbId
        IncomeRefTargetRewriter.Rewrite(analysis, idMap, logger);

        // 6. Load tax brackets (read-only query — no writes)
        var bracketsResult = await mediator.Send(new GetPricingTaxBracketsQuery(), cancellationToken);

        // 7. Run the exact same calculation as Save — no SaveChangesAsync anywhere
        // Pass user-supplied FinalValueRounded override (null = recompute).
        var result = calcService.Calculate(analysis, bracketsResult.Brackets, command.FinalValueRounded);
        analysis.ApplyCalculationResult(result);

        // 8. Return DTO — the analysis object is garbage-collected after this return
        return new PreviewIncomeAnalysisResult(IncomeAnalysisMapper.ToDto(analysis));
    }

    // ── Validation — identical to SaveIncomeAnalysisCommandHandler ───────────

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

    // ── Domain tree builder — same shape as SaveIncomeAnalysisCommandHandler.BuildSections ──
    // Each entity receives a fresh Guid immediately (no EF flush required).

    // When the input carries a valid Guid clientId, reuse it as the in-memory Id.
    // This makes preview deterministic — identical input → identical response Ids,
    // which lets the frontend hash-compare payloads and break the response→reset→re-request loop.
    private static Guid IdFromClientIdOrNew(string? clientId) =>
        Guid.TryParse(clientId, out var g) ? g : Guid.NewGuid();

    private static List<IncomeSection> BuildSectionsWithIds(
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
                sInput.DisplaySeq,
                id: IdFromClientIdOrNew(sInput.ClientId));

            var categories = BuildCategoriesWithIds(section.Id, sInput.Categories);
            section.ReplaceCategories(categories);
            sections.Add(section);
        }
        return sections;
    }

    private static List<IncomeCategory> BuildCategoriesWithIds(
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
                cInput.DisplaySeq,
                id: IdFromClientIdOrNew(cInput.ClientId));

            var assumptions = BuildAssumptionsWithIds(category.Id, cInput.Assumptions);
            category.ReplaceAssumptions(assumptions);
            categories.Add(category);
        }
        return categories;
    }

    private static List<IncomeAssumption> BuildAssumptionsWithIds(
        Guid categoryId,
        IReadOnlyList<IncomeAssumptionInput> inputs)
    {
        var assumptions = new List<IncomeAssumption>();
        foreach (var aInput in inputs)
        {
            var assumption = IncomeAssumption.Create(
                categoryId,
                aInput.AssumptionType,
                aInput.AssumptionName,
                aInput.Identifier,
                aInput.DisplaySeq,
                aInput.MethodTypeCode,
                aInput.Detail.GetRawText(),
                id: IdFromClientIdOrNew(aInput.ClientId));

            assumptions.Add(assumption);
        }
        return assumptions;
    }
}
