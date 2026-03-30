namespace Appraisal.Contracts.Appraisals.Dto;

public record EncroachmentDto(
    bool? IsEncroached,
    string? IsEncroachedRemark,
    decimal? EncroachArea
);
