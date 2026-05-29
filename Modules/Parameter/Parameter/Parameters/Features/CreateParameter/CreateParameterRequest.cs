public record CreateParameterRequest(
    string Group,
    string Country,
    string Language,
    string Code,
    string DescriptionTh,
    string DescriptionEn,
    bool IsActive,
    int SeqNo
);
