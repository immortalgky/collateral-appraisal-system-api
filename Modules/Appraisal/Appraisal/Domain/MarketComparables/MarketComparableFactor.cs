namespace Appraisal.Domain.MarketComparables;

/// <summary>
/// Master definition of configurable factors for market comparables.
/// Defines field metadata including data type, length, and parameter options.
/// </summary>
public class MarketComparableFactor : Entity<Guid>
{
    public string FactorCode { get; private set; } = null!;
    public string FieldName { get; private set; } = null!;
    public FactorDataType DataType { get; private set; }
    public int? FieldLength { get; private set; }
    public int? FieldDecimal { get; private set; }
    public string? ParameterGroup { get; private set; }
    public bool IsActive { get; private set; } = true;

    private readonly List<MarketComparableFactorTranslation> _translations = [];
    public IReadOnlyList<MarketComparableFactorTranslation> Translations => _translations.AsReadOnly();

    private MarketComparableFactor() { }

    public static MarketComparableFactor Create(
        string factorCode,
        string fieldName,
        FactorDataType dataType,
        IEnumerable<(string Language, string FactorName)> translations,
        int? fieldLength = null,
        int? fieldDecimal = null,
        string? parameterGroup = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(factorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        ValidateDataTypeConstraints(dataType, fieldDecimal, parameterGroup);
        ValidateTranslations(translations);

        var factor = new MarketComparableFactor
        {
            Id = Guid.CreateVersion7(),
            FactorCode = factorCode.ToUpperInvariant(),
            FieldName = fieldName,
            DataType = dataType,
            FieldLength = fieldLength,
            FieldDecimal = fieldDecimal,
            ParameterGroup = parameterGroup,
            IsActive = true
        };

        foreach (var (language, factorName) in translations)
            factor._translations.Add(MarketComparableFactorTranslation.Create(language, factorName));

        return factor;
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

    private static void ValidateTranslations(IEnumerable<(string Language, string FactorName)> translations)
    {
        var list = translations.ToList();

        if (list.Count == 0)
            throw new ArgumentException("At least one translation is required.");

        if (!list.Any(t => t.Language.Equals("en", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("English (en) translation is required.");

        var duplicates = list.GroupBy(t => t.Language.ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        if (duplicates.Any())
            throw new ArgumentException($"Duplicate language(s): {string.Join(", ", duplicates)}");
    }

    public void Update(
        string fieldName,
        FactorDataType dataType,
        int? fieldLength,
        int? fieldDecimal,
        string? parameterGroup,
        IEnumerable<(string Language, string FactorName)> translations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ValidateDataTypeConstraints(dataType, fieldDecimal, parameterGroup);
        ValidateTranslations(translations);

        FieldName = fieldName;
        DataType = dataType;
        FieldLength = fieldLength;
        FieldDecimal = fieldDecimal;
        ParameterGroup = parameterGroup;

        _translations.Clear();
        foreach (var (language, factorName) in translations)
            _translations.Add(MarketComparableFactorTranslation.Create(language, factorName));
    }

    public string GetFactorName(string language)
    {
        var translation = _translations.FirstOrDefault(
            t => t.Language.Equals(language, StringComparison.OrdinalIgnoreCase));

        return translation?.FactorName
            ?? _translations.First(t => t.Language.Equals("en", StringComparison.OrdinalIgnoreCase)).FactorName;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
