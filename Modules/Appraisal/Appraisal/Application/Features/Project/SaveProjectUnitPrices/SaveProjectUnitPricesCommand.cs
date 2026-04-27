using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Project.SaveProjectUnitPrices;

/// <summary>Command to save location flags for project unit prices.</summary>
public record SaveProjectUnitPricesCommand(
    Guid AppraisalId,
    List<ProjectUnitPriceFlagData> UnitPriceFlags
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
