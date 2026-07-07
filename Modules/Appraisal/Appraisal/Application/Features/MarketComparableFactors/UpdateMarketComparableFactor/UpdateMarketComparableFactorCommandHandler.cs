using Appraisal.Domain.MarketComparables;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableFactors.UpdateMarketComparableFactor;

/// <summary>
/// Handles updating an existing market comparable factor.
/// </summary>
internal sealed class UpdateMarketComparableFactorCommandHandler :
    ICommandHandler<UpdateMarketComparableFactorCommand, UpdateMarketComparableFactorResult>
{
    private readonly IMarketComparableFactorRepository _repository;
    private readonly IAppraisalUnitOfWork _unitOfWork;

    public UpdateMarketComparableFactorCommandHandler(
        IMarketComparableFactorRepository repository,
        IAppraisalUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<UpdateMarketComparableFactorResult> Handle(
        UpdateMarketComparableFactorCommand command,
        CancellationToken cancellationToken)
    {
        var factor = await _repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Market comparable factor with ID '{command.Id}' not found.");

        // Parse DataType enum
        if (!Enum.TryParse<FactorDataType>(command.DataType, ignoreCase: true, out var dataType))
            throw new ArgumentException(
                $"Invalid DataType value: {command.DataType}. Valid values are: {string.Join(", ", Enum.GetNames<FactorDataType>())}");

        // Update the entity using domain method
        factor.Update(
            command.FieldName,
            dataType,
            command.FieldLength,
            command.FieldDecimal,
            command.ParameterGroup,
            command.Translations);

        // Only change active state when the caller explicitly supplies it; a partial payload
        // that omits IsActive (null) must leave the factor's current state untouched.
        if (command.IsActive is true)
            factor.Activate();
        else if (command.IsActive is false)
            factor.Deactivate();

        await _repository.UpdateAsync(factor, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateMarketComparableFactorResult(factor.Id);
    }
}
