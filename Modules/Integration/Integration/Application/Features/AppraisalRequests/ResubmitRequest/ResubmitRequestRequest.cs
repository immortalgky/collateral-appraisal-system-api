namespace Integration.Application.Features.AppraisalRequests.ResubmitRequest;
using Request.Contracts.RequestDocuments.Dto;
using Request.Contracts.Requests.Dtos;
public record ResubmitRequestRequest(
  string? UploadSessionId,
  Guid RequestId,
  string Purpose,
  string Channel,
  UserInfoDto Requestor,
  UserInfoDto Creator,
  string Priority,
  bool IsPma,
  RequestDetailDto Detail,
  List<RequestCustomerDto> Customers,
  List<RequestPropertyDto> Properties,
  List<RequestTitleDto> Titles,
  List<RequestDocumentDto> Documents,
  List<RequestCommentDto> Comments
);
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