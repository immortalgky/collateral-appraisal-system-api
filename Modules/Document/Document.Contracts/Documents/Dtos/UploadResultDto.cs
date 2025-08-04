namespace Document.Contracts.Documents.Dtos;

public record UploadResultDto(
    bool IsSuccess,
    string Comment = ""
);