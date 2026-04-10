namespace Request.Domain.Requests;

public class RequestStatus : ValueObject
{
    public string Code { get; }
    public static RequestStatus Draft => new("DRAFT");
    public static RequestStatus New => new("NEW");
    public static RequestStatus Submitted => new("SUBMITTED");
    public static RequestStatus Assigned => new("ASSIGNED");
    public static RequestStatus InProgress => new("IN_PROGRESS");
    public static RequestStatus Completed => new("COMPLETED");
    public static RequestStatus Cancelled => new("CANCELLED");

    private RequestStatus(string code)
    {
        Code = code;
    }

    public static RequestStatus FromString(string code)
    {
        return code switch
        {
            "DRAFT" => Draft,
            "NEW" => New,
            "SUBMITTED" => Submitted,
            "ASSIGNED" => Assigned,
            "IN_PROGRESS" => InProgress,
            "INPROGRESS" => InProgress,
            "COMPLETED" => Completed,
            "CANCELLED" => Cancelled,
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