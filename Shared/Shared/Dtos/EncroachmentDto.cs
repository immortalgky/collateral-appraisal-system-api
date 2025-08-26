namespace Shared.Dtos;

public record EncroachmentDto(
    bool? IsEncroached,
    string? IsEncroachedRemark,
    decimal? EncroachArea
);
