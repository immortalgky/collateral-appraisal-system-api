namespace Request.Domain.Requests;

public class RequestNumber : ValueObject
{
    public string Value { get; }

    private RequestNumber(string value)
    {
        Value = value;
    }

    public static RequestNumber Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Request number cannot be null or empty.", nameof(value));

        return new RequestNumber(value);
    }

    public override string ToString()
    {
        return Value;
    }

    public static implicit operator string(RequestNumber requestNumber)
    {
        return requestNumber.Value;
    }
}