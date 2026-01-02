namespace Request.Domain.Requests;

public class AppraisalNumber : ValueObject
{
    public string Value { get; }

    private AppraisalNumber(string value)
    {
        Value = value;
    }

    public static AppraisalNumber Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Appraisal number cannot be null or empty.", nameof(value));
        }

        return new AppraisalNumber(value);
    }

    public override string ToString() => Value;
    public static implicit operator string(AppraisalNumber appraisalNumber) => appraisalNumber.Value;
}