namespace Request.Contracts.Requests.Dtos;

public record SoftDeleteDto(
    bool IsDeleted,
    DateTime? DeletedOn,
    string? DeletedBy
);
