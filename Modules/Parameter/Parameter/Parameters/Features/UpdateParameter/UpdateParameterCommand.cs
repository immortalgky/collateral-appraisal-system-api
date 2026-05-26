namespace Parameter.Parameters.Features.UpdateParameter;

public record UpdateParameterCommand(
    long ParId,
    string? Country,
    string? Language,
    string? Code,
    string? Description,
    bool? IsActive,
    int? SeqNo
) : ICommand<UpdateParameterResult>;
