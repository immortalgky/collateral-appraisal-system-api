namespace Integration.Application.Features.AppraisalRequests.ResubmitRequest;

public class ResubmitRequestResult
{
    public string Status { get; }
    public string Message { get; }
    public string? ErrorCode { get; }

    public ResubmitRequestResult(string status, string message, string? errorCode = null)
    {
        Status = status;
        Message = message;
        ErrorCode = errorCode;
    }
}
