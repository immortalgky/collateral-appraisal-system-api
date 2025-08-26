namespace Shared.Dtos;

public record ExpropriationDto(
    bool? IsExpropriate,
    string? IsExpropriateRemark,
    bool? InLineExpropriate,
    string? InLineExpropriateRemark,
    string? RoyalDecree
);
