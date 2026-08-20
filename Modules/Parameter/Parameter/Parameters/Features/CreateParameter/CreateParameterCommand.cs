public record CreateParameterCommand(
    string Group,
    string Country,
    string Language,
    string Code,
    string Description,
    bool IsActive,
    int SeqNo
) : ICommand<CreateParameterResult>;
