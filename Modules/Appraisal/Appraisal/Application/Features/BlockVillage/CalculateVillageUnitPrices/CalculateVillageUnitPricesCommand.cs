using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.BlockVillage.CalculateVillageUnitPrices;

public record CalculateVillageUnitPricesCommand(
    Guid AppraisalId
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
