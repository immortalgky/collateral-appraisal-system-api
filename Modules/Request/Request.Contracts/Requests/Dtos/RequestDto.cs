namespace Request.Contracts.Requests.Dtos;

public record RequestDto(
    long Id,
    string AppraisalNo,
    string Status,
    RequestDetailDto Detail
);