using Appraisal.Domain.MarketComparables;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableFactors.CreateMarketComparableFactor;

/// <summary>
/// Handles the creation of a new market comparable factor.
/// </summary>
internal sealed class CreateMarketComparableFactorCommandHandler :
    ICommandHandler<CreateMarketComparableFactorCommand, CreateMarketComparableFactorResult>
{
    private readonly IMarketComparableFactorRepository _repository;
    private readonly IAppraisalUnitOfWork _unitOfWork;

    public CreateMarketComparableFactorCommandHandler(
        IMarketComparableFactorRepository repository,
        IAppraisalUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateMarketComparableFactorResult> Handle(
        CreateMarketComparableFactorCommand command,
        CancellationToken cancellationToken)
    {
        // Parse DataType enum
        if (!Enum.TryParse<FactorDataType>(command.DataType, ignoreCase: true, out var dataType))
        {
            throw new ArgumentException(
                $"Invalid DataType value: {command.DataType}. Valid values are: {string.Join(", ", Enum.GetNames<FactorDataType>())}");
        }

        // Check if factor code already exists
        var existingFactor = await _repository.GetByCodeAsync(command.FactorCode, cancellationToken);
        if (existingFactor != null)
        {
            throw new InvalidOperationException($"Factor with code '{command.FactorCode}' already exists.");
        }

        // Create the entity using factory method
        var factor = MarketComparableFactor.Create(
            command.FactorCode,
            command.FactorName,
            command.FieldName,
            dataType,
            command.FieldLength,
            command.FieldDecimal,
            command.ParameterGroup);

        await _repository.AddAsync(factor, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateMarketComparableFactorResult(factor.Id);
    }
}
