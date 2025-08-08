namespace Request.Requests.Features.GetRequestById;

public record GetRequestByIdResponse(
    long Id,
    string AppraisalNo,
    string Status,
    RequestDetailDto Detail);