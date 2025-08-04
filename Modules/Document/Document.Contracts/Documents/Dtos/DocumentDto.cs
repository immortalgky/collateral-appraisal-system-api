namespace Document.Contracts.Documents.Dtos;

public record DocumentDto(
    long Id,
    string RelateRequest,
    long RelateId,
    string DocType,
    string Filename,
    DateTime UploadTime,
    string Prefix,
    short Set,
    string Comment,
    string FilePath 
);