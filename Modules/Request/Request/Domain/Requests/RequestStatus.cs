namespace Request.Domain.Requests;

public class RequestStatus : ValueObject
{
    public string Code { get; }
    public static RequestStatus Draft => new(nameof(Draft).ToUpper());
    public static RequestStatus New => new(nameof(New).ToUpper());
    public static RequestStatus Submitted => new(nameof(Submitted).ToUpper());
    public static RequestStatus Assigned => new(nameof(Assigned).ToUpper());
    public static RequestStatus InProgress => new(nameof(InProgress).ToUpper());
    public static RequestStatus Completed => new(nameof(Completed).ToUpper());
    public static RequestStatus Cancelled => new(nameof(Cancelled).ToUpper());

    private RequestStatus(string code)
    {
        Code = code;
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