namespace Shared.Dtos;

public record UploadResultDto(
    bool IsSuccess,
    string FileName,
    string Detail
);