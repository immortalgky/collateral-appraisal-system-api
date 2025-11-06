namespace Request.Requests.ValueObjects;

public class RequestNumber : ValueObject
{
    public string Value { get; }
    private RequestNumber()
    {
        // EF core
    }
    
    private RequestNumber(string value)
    {
        Value = value;
    }

    public static RequestNumber Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Appraisal number cannot be null or empty.", nameof(value));
        }

        return new RequestNumber(value);
    }

    public override string ToString() => Value;
    public static implicit operator string(RequestNumber requestNumber) => requestNumber.Value;
}
