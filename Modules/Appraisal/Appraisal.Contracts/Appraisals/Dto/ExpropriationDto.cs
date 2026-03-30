namespace Appraisal.Contracts.Appraisals.Dto;

public record ExpropriationDto(
    bool? IsExpropriate,
    string? IsExpropriateRemark,
    bool? InLineExpropriate,
    string? InLineExpropriateRemark,
    string? RoyalDecree
);
