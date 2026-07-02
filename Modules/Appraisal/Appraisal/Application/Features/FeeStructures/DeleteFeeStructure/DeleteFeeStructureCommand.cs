using MediatR;

namespace Appraisal.Application.Features.FeeStructures.DeleteFeeStructure;

public record DeleteFeeStructureCommand(Guid Id)
    : ICommand<Unit>, ITransactionalCommand<IAppraisalUnitOfWork>;
