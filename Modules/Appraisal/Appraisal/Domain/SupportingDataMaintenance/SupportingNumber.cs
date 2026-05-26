namespace Appraisal.Domain.SupportingDataMaintenance;

public class SupportingNumber : ValueObject
{
    public string Value { get; }
    private SupportingNumber(string value) => Value = value;

    public static SupportingNumber Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Supporting number cannot be null or empty.", nameof(value));
        return new SupportingNumber(value);
    }

    public override string ToString() => Value;
    public static implicit operator string(SupportingNumber n) => n.Value;
}