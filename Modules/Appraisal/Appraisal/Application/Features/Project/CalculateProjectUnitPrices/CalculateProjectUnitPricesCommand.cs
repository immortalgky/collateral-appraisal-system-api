using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Project.CalculateProjectUnitPrices;

/// <summary>Command to calculate unit prices for a project (Condo or LandAndBuilding).</summary>
/// <param name="RecomputeValuation">
/// False only for a data correction: the prices are stored, but the approved appraisal total and its
/// value-changed event are left as they were (see CorrectProjectDataCommandHandler).
/// </param>
public record CalculateProjectUnitPricesCommand(
    Guid AppraisalId,
    bool RecomputeValuation = true
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
