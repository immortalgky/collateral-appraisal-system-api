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
                CostItemKind.PublicUtilityConstruction,
                "Public Utility Construction", seq++);
            analysis.AddCostItem(HypothesisCostCategory.ProjectDevCost,
                CostItemKind.LandFilling,
                "Land Filling", seq++);

            // Project Cost defaults
            seq = 1;
            analysis.AddCostItem(HypothesisCostCategory.ProjectCost,
                CostItemKind.AllocationPermitFee,
                "Allocation Permit Fee", seq++);
            analysis.AddCostItem(HypothesisCostCategory.ProjectCost,
                CostItemKind.LandTitleDeedDivisionFee,
                "Land Title Deed Division Fee", seq++);
            analysis.AddCostItem(HypothesisCostCategory.ProjectCost,
                CostItemKind.ProfessionalFee,
                "Professional Service Fees", seq++);
            analysis.AddCostItem(HypothesisCostCategory.ProjectCost,
                CostItemKind.AdminFee,
                "Project Admin/Management", seq++);
            analysis.AddCostItem(HypothesisCostCategory.ProjectCost,
                CostItemKind.SellingAdvertising,
                "Selling/Advertising", seq++);

            // Government Tax defaults
            seq = 1;
            analysis.AddCostItem(HypothesisCostCategory.GovernmentTax,
                CostItemKind.TransferFee,
                "Transfer Fee", seq++);
            analysis.AddCostItem(HypothesisCostCategory.GovernmentTax,
                CostItemKind.SpecificBusinessTax,
                "Specific Business Tax", seq++);
        }
        else // Condominium
        {
            // Hard Cost defaults
            int seq = 1;
            analysis.AddCostItem(HypothesisCostCategory.HardCost,
                CostItemKind.CondoBuildingConstruction,
                "Condo Building Construction", seq++);
            analysis.AddCostItem(HypothesisCostCategory.HardCost,
                CostItemKind.Furniture,
                "Furniture/Kitchen Sets/Air Conditioners", seq++);
            analysis.AddCostItem(HypothesisCostCategory.HardCost,
                CostItemKind.ExternalUtilities,
                "External Utilities", seq++);

            // Soft Cost defaults
            seq = 1;
            analysis.AddCostItem(HypothesisCostCategory.SoftCost,
                CostItemKind.ProfessionalFee,
                "Professional Service Fees", seq++);
            analysis.AddCostItem(HypothesisCostCategory.SoftCost,
                CostItemKind.AdminFee,
                "Project Admin/Management", seq++);
            analysis.AddCostItem(HypothesisCostCategory.SoftCost,
                CostItemKind.SellingAdvertising,
                "Selling/Advertising", seq++);
            analysis.AddCostItem(HypothesisCostCategory.SoftCost,
                CostItemKind.CondoTitleDeedFee,
                "Condo Title Deed Issuance Fee", seq++);
            analysis.AddCostItem(HypothesisCostCategory.SoftCost,
                CostItemKind.EIA,
                "EIA Report", seq++);
            analysis.AddCostItem(HypothesisCostCategory.SoftCost,
                CostItemKind.CondoRegistrationFee,
                "Condo Registration Permit Fee", seq++);

            // Condo Gov Tax defaults
            seq = 1;
            analysis.AddCostItem(HypothesisCostCategory.CondoGovTax,
                CostItemKind.TransferFee,
                "Transfer Fee", seq++);
            analysis.AddCostItem(HypothesisCostCategory.CondoGovTax,
                CostItemKind.SpecificBusinessTax,
                "Specific Business Tax", seq++);
        }
    }
}
