namespace Request.Contracts.Requests.Dtos;

public record ReferenceDto(
    string? PrevAppraisalNo,
    decimal? PrevAppraisalValue,
    DateTime? PrevAppraisalDate
);