
public class CreateParameterCommandHandler(
    IParameterRepository parameterRepository
) : ICommandHandler<CreateParameterCommand, CreateParameterResult>
{
    public async Task<CreateParameterResult> Handle(
    CreateParameterCommand command,
    CancellationToken cancellationToken)
{
    var paramTh = Parameter.Parameters.Models.Parameter.Create(
        group: command.Group,
        country: command.Country,
        language: "TH",
        code: command.Code,
        description: command.DescriptionTh,
        isActive: command.IsActive,
        seqNo: command.SeqNo
    );

    var paramEn = Parameter.Parameters.Models.Parameter.Create(
        group: command.Group,
        country: command.Country,
        language: "EN",
        code: command.Code,
        description: command.DescriptionEn,
        isActive: command.IsActive,
        seqNo: command.SeqNo
    );

    await parameterRepository.AddAsync(paramTh, cancellationToken);
    await parameterRepository.AddAsync(paramEn, cancellationToken);
    await parameterRepository.SaveChangesAsync(cancellationToken);

    return new CreateParameterResult(paramTh.Id);
}
}