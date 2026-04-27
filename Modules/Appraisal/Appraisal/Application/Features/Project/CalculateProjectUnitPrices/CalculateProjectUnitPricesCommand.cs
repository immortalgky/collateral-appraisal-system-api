using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Project.CalculateProjectUnitPrices;

/// <summary>Command to calculate unit prices for a project (Condo or LandAndBuilding).</summary>
public record CalculateProjectUnitPricesCommand(
    Guid AppraisalId
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
