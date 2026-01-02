namespace Request.Contracts.Requests.Dtos;

public record UploadResultDto(
    bool IsSuccess,
    string FileName,
    string Detail
);