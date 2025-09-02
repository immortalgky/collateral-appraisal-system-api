namespace Shared.Dtos;

public record ReferenceDto(
    string? PrevAppraisalNo,
    decimal? PrevAppraisalValue,
    DateTime? PrevAppraisalDate
);