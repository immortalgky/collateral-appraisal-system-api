using Appraisal.Domain.Appraisals;

namespace Appraisal.Domain.Services;

/// <summary>
/// Recalculates derived fields on a pricing method's entities.
/// Backend acts as the "gate of truth" — frontend calculates for UX,
/// backend recalculates on save to verify.
/// </summary>
public interface IPricingCalculationService
{
    void Recalculate(PricingAnalysisMethod method);
}
