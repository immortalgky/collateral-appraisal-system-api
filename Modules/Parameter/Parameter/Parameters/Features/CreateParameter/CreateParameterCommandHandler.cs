
public class CreateParameterCommandHandler(
    IParameterRepository parameterRepository
) : ICommandHandler<CreateParameterCommand, CreateParameterResult>
{
    public async Task<CreateParameterResult> Handle(
    CreateParameterCommand command,
    CancellationToken cancellationToken)
{
    var param = Parameter.Parameters.Models.Parameter.Create(
        group: command.Group,
        country: command.Country,
        language: command.Language,
        code: command.Code,
        description: command.Description,
        isActive: command.IsActive,
        seqNo: command.SeqNo
    );

    await parameterRepository.AddAsync(param, cancellationToken);
    await parameterRepository.SaveChangesAsync(cancellationToken);

    return new CreateParameterResult(param.Id);
}
}