namespace Request.Domain.Requests;

public class RequestStatus : ValueObject
{
    public string Code { get; }

    private RequestStatus(string code)
    {
        Code = code;
    }

    public static RequestStatus Draft => new("Draft");
    public static RequestStatus New => new("New");
    public static RequestStatus Submitted => new("Submitted");
    public static RequestStatus Assigned => new("Assigned");
    public static RequestStatus InProgress => new("InProgress");
    public static RequestStatus Completed => new("Completed");
    public static RequestStatus Cancelled => new("Cancelled");

    public static RequestStatus FromString(string code)
    {
        return code switch
        {
            "Draft" => Draft,
            "New" => New,
            "Submitted" => Submitted,
            "Assigned" => Assigned,
            "InProgress" => InProgress,
            "Completed" => Completed,
            "Cancelled" => Cancelled,
            _ => throw new ArgumentException($"Invalid request status: {code}")
        };
    }

    public override string ToString()
    {
        return Code;
    }

    public static implicit operator string(RequestStatus requestStatus)
    {
        return requestStatus.Code;
    }
}
