using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.BlockCondo.CalculateCondoUnitPrices;

public record CalculateCondoUnitPricesCommand(
    Guid AppraisalId
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
