using Appraisal.Domain.MarketComparables;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableFactors.DeleteMarketComparableFactor;

/// <summary>
/// Handles soft deletion of a market comparable factor.
/// </summary>
internal sealed class DeleteMarketComparableFactorCommandHandler :
    ICommandHandler<DeleteMarketComparableFactorCommand, DeleteMarketComparableFactorResult>
{
    private readonly IMarketComparableFactorRepository _repository;
    private readonly IAppraisalUnitOfWork _unitOfWork;

    public DeleteMarketComparableFactorCommandHandler(
        IMarketComparableFactorRepository repository,
        IAppraisalUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<DeleteMarketComparableFactorResult> Handle(
        DeleteMarketComparableFactorCommand command,
        CancellationToken cancellationToken)
    {
        var factor = await _repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Market comparable factor with ID '{command.Id}' not found.");

        // Soft delete using domain method
        factor.Deactivate();

        await _repository.UpdateAsync(factor, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new DeleteMarketComparableFactorResult(true);
    }
}
