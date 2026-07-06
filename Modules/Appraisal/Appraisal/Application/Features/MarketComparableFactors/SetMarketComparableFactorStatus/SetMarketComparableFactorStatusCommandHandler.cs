using Appraisal.Domain.MarketComparables;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableFactors.SetMarketComparableFactorStatus;

/// <summary>
/// Handles activating / deactivating a market comparable factor.
/// </summary>
internal sealed class SetMarketComparableFactorStatusCommandHandler :
    ICommandHandler<SetMarketComparableFactorStatusCommand, SetMarketComparableFactorStatusResult>
{
    private readonly IMarketComparableFactorRepository _repository;
    private readonly IAppraisalUnitOfWork _unitOfWork;

    public SetMarketComparableFactorStatusCommandHandler(
        IMarketComparableFactorRepository repository,
        IAppraisalUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<SetMarketComparableFactorStatusResult> Handle(
        SetMarketComparableFactorStatusCommand command,
        CancellationToken cancellationToken)
    {
        var factor = await _repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Market comparable factor with ID '{command.Id}' not found.");

        if (command.IsActive)
            factor.Activate();
        else
            factor.Deactivate();

        await _repository.UpdateAsync(factor, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SetMarketComparableFactorStatusResult(true);
    }
}
