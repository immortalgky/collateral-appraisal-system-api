namespace Appraisal.Domain.MarketComparables;

/// <summary>
/// Master definition of configurable factors for market comparables.
/// Defines field metadata including data type, length, and parameter options.
/// </summary>
public class MarketComparableFactor : Entity<Guid>
{
    public string FactorCode { get; private set; } = null!;
    public string FactorName { get; private set; } = null!;
    public string FieldName { get; private set; } = null!;
    public FactorDataType DataType { get; private set; }
    public int? FieldLength { get; private set; }
    public int? FieldDecimal { get; private set; }
    public string? ParameterGroup { get; private set; }
    public bool IsActive { get; private set; } = true;

    private MarketComparableFactor() { }

    public static MarketComparableFactor Create(
        string factorCode,
        string factorName,
        string fieldName,
        FactorDataType dataType,
        int? fieldLength = null,
        int? fieldDecimal = null,
        string? parameterGroup = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(factorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(factorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        ValidateDataTypeConstraints(dataType, fieldDecimal, parameterGroup);

        return new MarketComparableFactor
        {
            Id = Guid.NewGuid(),
            FactorCode = factorCode.ToUpperInvariant(),
            FactorName = factorName,
            FieldName = fieldName,
            DataType = dataType,
            FieldLength = fieldLength,
            FieldDecimal = fieldDecimal,
            ParameterGroup = parameterGroup,
            IsActive = true
        };
    }

    private static void ValidateDataTypeConstraints(
        FactorDataType dataType,
        int? fieldDecimal,
        string? parameterGroup)
    {
        if (dataType == FactorDataType.Numeric && fieldDecimal.HasValue && fieldDecimal < 0)
            throw new ArgumentException("FieldDecimal cannot be negative for Numeric type.");

        if ((dataType == FactorDataType.Dropdown || dataType == FactorDataType.Radio)
            && string.IsNullOrWhiteSpace(parameterGroup))
            throw new ArgumentException("ParameterGroup is required for Dropdown/Radio data types.");
    }

    public void Update(
        string factorName,
        string fieldName,
        int? fieldLength,
        int? fieldDecimal,
        string? parameterGroup)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(factorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        FactorName = factorName;
        FieldName = fieldName;
        FieldLength = fieldLength;
        FieldDecimal = fieldDecimal;
        ParameterGroup = parameterGroup;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
