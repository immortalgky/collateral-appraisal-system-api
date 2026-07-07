using Appraisal.Domain.MarketComparables;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableFactors.DeleteMarketComparableFactor;

/// <summary>
/// Handles permanent deletion of a market comparable factor.
/// Blocked with a 409 when the factor is still referenced by a template
/// (use deactivate instead in that case).
/// </summary>
internal sealed class DeleteMarketComparableFactorCommandHandler :
    ICommandHandler<DeleteMarketComparableFactorCommand, DeleteMarketComparableFactorResult>
{
    private readonly IMarketComparableFactorRepository _repository;
    private readonly IAppraisalUnitOfWork _unitOfWork;
    private readonly AppraisalDbContext _dbContext;

    public DeleteMarketComparableFactorCommandHandler(
        IMarketComparableFactorRepository repository,
        IAppraisalUnitOfWork unitOfWork,
        AppraisalDbContext dbContext)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _dbContext = dbContext;
    }

    public async Task<DeleteMarketComparableFactorResult> Handle(
        DeleteMarketComparableFactorCommand command,
        CancellationToken cancellationToken)
    {
        var factor = await _repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException("MarketComparableFactor", command.Id);

        var isUsedInTemplate = await _dbContext.MarketComparableTemplateFactors
            .AnyAsync(tf => tf.FactorId == command.Id, cancellationToken);

        var isUsedInData = await _dbContext.MarketComparableData
            .AnyAsync(d => d.FactorId == command.Id, cancellationToken);

        if (isUsedInTemplate || isUsedInData)
            throw new ConflictException(
                "This factor is in use (by a template or existing market comparable data) and cannot be deleted. Deactivate it instead.");

        await _repository.DeleteAsync(factor, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new DeleteMarketComparableFactorResult(true);
    }
}
