using Appraisal.Application.Configurations;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Hypothesis;
using Appraisal.Domain.Appraisals.Hypothesis.CostItems;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.GenerateHypothesisAnalysis;

/// <summary>
/// Creates a new HypothesisAnalysis for the given method and seeds default cost rows
/// from the template defaults appropriate to the variant.
/// </summary>
public class GenerateHypothesisAnalysisCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<GenerateHypothesisAnalysisCommand, GenerateHypothesisAnalysisResult>
{
    public async Task<GenerateHypothesisAnalysisResult> Handle(
        GenerateHypothesisAnalysisCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
                                 command.PricingAnalysisId, cancellationToken)
                             ?? throw new InvalidOperationException(
                                 $"PricingAnalysis {command.PricingAnalysisId} not found");

        var method = pricingAnalysis.Approaches
                         .SelectMany(a => a.Methods)
                         .FirstOrDefault(m => m.Id == command.MethodId)
                     ?? throw new InvalidOperationException(
                         $"PricingAnalysisMethod {command.MethodId} not found");

        // If already exists, return existing (idempotent)
        if (method.HypothesisAnalysis is not null)
        {
            var existing = method.HypothesisAnalysis;
            return new GenerateHypothesisAnalysisResult(existing.Id, command.MethodId, existing.Variant);
        }

        var analysis = HypothesisAnalysis.Create(command.MethodId, command.Variant);
        method.SetHypothesisAnalysis(analysis);

        // Seed default cost items based on variant
        SeedDefaultCostItems(analysis, command.Variant);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new GenerateHypothesisAnalysisResult(analysis.Id, command.MethodId, command.Variant);
    }

    private static void SeedDefaultCostItems(HypothesisAnalysis analysis, HypothesisVariant variant)
    {
        if (variant == HypothesisVariant.LandBuilding)
        {
            // Project Dev Cost defaults
            int seq = 1;
            analysis.AddCostItem(HypothesisCostCategory.ProjectDevCost,
                "Public Utility Construction", seq++);
            analysis.AddCostItem(HypothesisCostCategory.ProjectDevCost,
                "Land Filling", seq++);

            // Project Cost defaults
            seq = 1;
            analysis.AddCostItem(HypothesisCostCategory.ProjectCost,
                "AllocationPermitFee", seq++);
            analysis.AddCostItem(HypothesisCostCategory.ProjectCost,
                "Land Title Deed Division Fee", seq++);
            analysis.AddCostItem(HypothesisCostCategory.ProjectCost,
                "Professional Service Fees", seq++);
            analysis.AddCostItem(HypothesisCostCategory.ProjectCost,
                "Project Admin/Management", seq++);
            analysis.AddCostItem(HypothesisCostCategory.ProjectCost,
                "Selling/Advertising", seq++);

            // Government Tax defaults
            seq = 1;
            analysis.AddCostItem(HypothesisCostCategory.GovernmentTax,
                "Transfer Fee", seq++);
            analysis.AddCostItem(HypothesisCostCategory.GovernmentTax,
                "Specific Business Tax", seq++);
        }
        else // Condominium
        {
            // Hard Cost defaults
            int seq = 1;
            analysis.AddCostItem(HypothesisCostCategory.HardCost,
                "Condo Building Construction", seq++);
            analysis.AddCostItem(HypothesisCostCategory.HardCost,
                "Furniture/Kitchen Sets/Air Conditioners", seq++);
            analysis.AddCostItem(HypothesisCostCategory.HardCost,
                "External Utilities", seq++);

            // Soft Cost defaults
            seq = 1;
            analysis.AddCostItem(HypothesisCostCategory.SoftCost,
                "Professional Service Fees", seq++);
            analysis.AddCostItem(HypothesisCostCategory.SoftCost,
                "Project Admin/Management", seq++);
            analysis.AddCostItem(HypothesisCostCategory.SoftCost,
                "Selling/Advertising", seq++);
            analysis.AddCostItem(HypothesisCostCategory.SoftCost,
                "Condo Title Deed Issuance Fee", seq++);
            analysis.AddCostItem(HypothesisCostCategory.SoftCost,
                "EIA Report", seq++);
            analysis.AddCostItem(HypothesisCostCategory.SoftCost,
                "Condo Registration Permit Fee", seq++);

            // Condo Gov Tax defaults
            seq = 1;
            analysis.AddCostItem(HypothesisCostCategory.CondoGovTax,
                "Transfer Fee", seq++);
            analysis.AddCostItem(HypothesisCostCategory.CondoGovTax,
                "Specific Business Tax", seq++);
        }
    }
}
