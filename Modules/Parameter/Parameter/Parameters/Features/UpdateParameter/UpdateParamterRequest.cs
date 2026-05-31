public record UpdateParameterRequest(
    long ParId,
    string? Country,
    string? Language,
    string? Code,
    string? Description,
    bool? IsActive,
    int? SeqNo
);
