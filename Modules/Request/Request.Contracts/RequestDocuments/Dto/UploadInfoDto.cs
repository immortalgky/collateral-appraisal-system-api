namespace Request.Contracts.RequestDocuments.Dto;

public record UploadInfoDto(long? UploadedBy, string? UploadedByName, DateTime? UploadedAt);
