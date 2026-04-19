using Appraisal.Application.Configurations;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Income;
using Appraisal.Domain.Appraisals.Income.MethodDetails;
using Appraisal.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Parameter.Contracts.PricingParameters;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.SaveIncomeAnalysis;

public class SaveIncomeAnalysisCommandHandler(
    IPricingAnalysisRepository repository,
    IAppraisalUnitOfWork unitOfWork,
    IncomeCalculationService calcService,
    ISender mediator,
    ILogger<SaveIncomeAnalysisCommandHandler> logger
) : ICommandHandler<SaveIncomeAnalysisCommand, SaveIncomeAnalysisResult>
{

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

        // 4. Create IncomeAnalysis on first save or update parameters on re-save
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

        analysis.SetFinalValueAdjust(command.FinalValueAdjust);

        analysis.SetHighestBestUsed(
            command.IsHighestBestUsed,
            Domain.Appraisals.Income.HighestBestUsed.Create(
                command.HighestBestUsed?.AreaRai,
                command.HighestBestUsed?.AreaNgan,
                command.HighestBestUsed?.AreaWa,
                command.HighestBestUsed?.PricePerSqWa),
            command.AppraisalPriceRounded);

        var (sectionPairs, categoryPairs, assumptionPairs) = SyncSections(analysis, command.Sections);

        // Flush INSERTs so EF populates entity Ids from NEWSEQUENTIALID() before the M13 rewriter builds idMap.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // 5. Build clientId → dbId map from the (input, entity) pairs produced by SyncXxx.
        // Using pairs avoids Zip-order misalignment when the user reorders sections or interleaves new + existing rows.
        var idMap = new Dictionary<Guid, Guid>();
        foreach (var (sInput, section) in sectionPairs)
            if (Guid.TryParse(sInput.ClientId, out var sCid)) idMap[sCid] = section.Id;
        foreach (var (cInput, category) in categoryPairs)
            if (Guid.TryParse(cInput.ClientId, out var cCid)) idMap[cCid] = category.Id;
        foreach (var (aInput, assumption) in assumptionPairs)
            if (Guid.TryParse(aInput.ClientId, out var aCid)) idMap[aCid] = assumption.Id;

        // Rewrite Method-13 refTarget.clientId → dbId for fresh trees (first save).
        // On re-save the rewriter skips nodes whose dbId is already populated.
        IncomeRefTargetRewriter.Rewrite(analysis, idMap, logger);

        // 6. Load tax brackets for Method-10 server-side derivation, then recalculate.
        var bracketsResult = await mediator.Send(new GetPricingTaxBracketsQuery(), cancellationToken);
        var result = calcService.Calculate(analysis, bracketsResult.Brackets);
        analysis.ApplyCalculationResult(result);

        // 7. Propagate the user's adjusted appraisal price up the chain.
        // Priority: AppraisalPriceRounded (explicit override) → derived appraisal price
        //           (FinalValueAdjust + HBU.TotalValue when applicable) → FinalValueRounded.
        decimal methodValue;
        if (analysis.AppraisalPriceRounded is > 0)
        {
            methodValue = analysis.AppraisalPriceRounded.Value;
        }
        else if (analysis.FinalValueAdjust.HasValue)
        {
            // Mirror the frontend TotalValue derivation:
            // totalWa = AreaRai*400 + AreaNgan*100 + AreaWa
            // hbuValue = totalWa * PricePerSqWa  (only when !IsHighestBestUsed)
            var hbuValue = 0m;
            if (!analysis.IsHighestBestUsed)
            {
                var hbu = analysis.HighestBestUsed;
                var totalWa = (hbu.AreaRai ?? 0) * 400m
                            + (hbu.AreaNgan ?? 0) * 100m
                            + (hbu.AreaWa ?? 0m);
                hbuValue = totalWa * (hbu.PricePerSqWa ?? 0m);
            }
            methodValue = analysis.FinalValueAdjust.Value + hbuValue;
        }
        else
        {
            methodValue = result.FinalValueRounded;
        }
        method.SetValue(methodValue);

        // Propagate up the approach/analysis chain if this method is selected
        if (method.IsSelected)
        {
            var parentApproach = pricingAnalysis.Approaches
                .First(a => a.Methods.Any(m => m.Id == method.Id));
            parentApproach.SetValue(methodValue);

            if (parentApproach.IsSelected)
                pricingAnalysis.SetFinalValues(methodValue);
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

    // ── Selective upsert: UPDATE existing, INSERT new, DELETE orphans ────────
    // clientId == entity.Id for rows the frontend received from a previous save.
    // New rows carry a fresh frontend-generated Guid that won't match any existing Id.
    // Pass 1 (upserts) runs before Pass 2 (deletes) so EF's cascade never deletes a row
    // we just updated.
    //
    // Each Sync method returns (input, entity) pairs in input order.
    // The handler walks these pairs after SaveChangesAsync to build the clientId→dbId map,
    // avoiding Zip-order misalignment when users reorder or interleave new + existing rows.

    private static (
        IReadOnlyList<(IncomeSectionInput, IncomeSection)> SectionPairs,
        IReadOnlyList<(IncomeCategoryInput, IncomeCategory)> CategoryPairs,
        IReadOnlyList<(IncomeAssumptionInput, IncomeAssumption)> AssumptionPairs)
        SyncSections(
            IncomeAnalysis analysis,
            IReadOnlyList<IncomeSectionInput> inputs)
    {
        var existingById = analysis.Sections.ToDictionary(s => s.Id);
        var processedIds = new HashSet<Guid>();

        var sectionPairs     = new List<(IncomeSectionInput, IncomeSection)>();
        var allCategoryPairs = new List<(IncomeCategoryInput, IncomeCategory)>();
        var allAssumptionPairs = new List<(IncomeAssumptionInput, IncomeAssumption)>();

        foreach (var sInput in inputs)
        {
            IncomeSection section;
            if (Guid.TryParse(sInput.ClientId, out var sId) && existingById.TryGetValue(sId, out var found))
            {
                found.Update(sInput.SectionType, sInput.SectionName, sInput.Identifier, sInput.DisplaySeq);
                section = found;
                processedIds.Add(sId);
            }
            else
            {
                section = IncomeSection.Create(
                    analysis.Id, sInput.SectionType, sInput.SectionName,
                    sInput.Identifier, sInput.DisplaySeq);
                analysis.AddSection(section);
            }
            sectionPairs.Add((sInput, section));

            var (catPairs, asmPairs) = SyncCategories(section, sInput.Categories);
            allCategoryPairs.AddRange(catPairs);
            allAssumptionPairs.AddRange(asmPairs);
        }

        // Only remove sections that existed BEFORE this sync started and were not matched.
        // Newly-added sections (Id = Guid.Empty) are not in existingById, so they're safe.
        foreach (var (id, section) in existingById)
            if (!processedIds.Contains(id))
                analysis.RemoveSection(section);

        return (sectionPairs, allCategoryPairs, allAssumptionPairs);
    }

    private static (
        IReadOnlyList<(IncomeCategoryInput, IncomeCategory)> CategoryPairs,
        IReadOnlyList<(IncomeAssumptionInput, IncomeAssumption)> AssumptionPairs)
        SyncCategories(
            IncomeSection section,
            IReadOnlyList<IncomeCategoryInput> inputs)
    {
        var existingById = section.Categories.ToDictionary(c => c.Id);
        var processedIds = new HashSet<Guid>();

        var categoryPairs    = new List<(IncomeCategoryInput, IncomeCategory)>();
        var allAssumptionPairs = new List<(IncomeAssumptionInput, IncomeAssumption)>();

        foreach (var cInput in inputs)
        {
            IncomeCategory category;
            if (Guid.TryParse(cInput.ClientId, out var cId) && existingById.TryGetValue(cId, out var found))
            {
                found.Update(cInput.CategoryType, cInput.CategoryName, cInput.Identifier, cInput.DisplaySeq);
                category = found;
                processedIds.Add(cId);
            }
            else
            {
                category = IncomeCategory.Create(
                    section.Id, cInput.CategoryType, cInput.CategoryName,
                    cInput.Identifier, cInput.DisplaySeq);
                section.AttachCategory(category);
            }
            categoryPairs.Add((cInput, category));

            var asmPairs = SyncAssumptions(category, cInput.Assumptions);
            allAssumptionPairs.AddRange(asmPairs);
        }

        // Only remove categories that existed BEFORE this sync started.
        foreach (var (id, category) in existingById)
            if (!processedIds.Contains(id))
                section.RemoveCategory(category);

        return (categoryPairs, allAssumptionPairs);
    }

    private static IReadOnlyList<(IncomeAssumptionInput, IncomeAssumption)> SyncAssumptions(
        IncomeCategory category,
        IReadOnlyList<IncomeAssumptionInput> inputs)
    {
        var existingById = category.Assumptions.ToDictionary(a => a.Id);
        var processedIds = new HashSet<Guid>();

        var pairs = new List<(IncomeAssumptionInput, IncomeAssumption)>();

        foreach (var aInput in inputs)
        {
            var detailJson = aInput.Detail.GetRawText();
            IncomeAssumption assumption;
            if (Guid.TryParse(aInput.ClientId, out var aId) && existingById.TryGetValue(aId, out var found))
            {
                found.Update(aInput.AssumptionType, aInput.AssumptionName,
                    aInput.Identifier, aInput.DisplaySeq, aInput.MethodTypeCode, detailJson);
                assumption = found;
                processedIds.Add(aId);
            }
            else
            {
                assumption = IncomeAssumption.Create(
                    category.Id, aInput.AssumptionType, aInput.AssumptionName,
                    aInput.Identifier, aInput.DisplaySeq, aInput.MethodTypeCode, detailJson);
                category.AttachAssumption(assumption);
            }
            pairs.Add((aInput, assumption));
        }

        // Only remove assumptions that existed BEFORE this sync started.
        foreach (var (id, assumption) in existingById)
            if (!processedIds.Contains(id))
                category.RemoveAssumption(assumption);

        return pairs;
    }
}
