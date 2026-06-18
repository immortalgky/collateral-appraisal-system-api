namespace Parameter.Parameters.Features.UpdateParameter;

public class UpdateParameterCommandHandler(
    IParameterRepository parameterRepository
) : ICommandHandler<UpdateParameterCommand,UpdateParameterResult>
{
    public async Task<UpdateParameterResult> Handle(
        UpdateParameterCommand command,
        CancellationToken cancellationToken)
    {
        var parameter = await parameterRepository.GetParameterByParId(command.ParId, cancellationToken)
            ?? throw new InvalidOperationException($"Parameter Id: {command.ParId} is not found.");

        parameter.Update(
            code: command.Code ?? parameter.Code,
            description: command.Description ?? parameter.Description,
            country: command.Country ?? parameter.Country,
            language: command.Language ?? parameter.Language,
            isActive: command.IsActive ?? parameter.IsActive,
            seqNo: command.SeqNo ?? parameter.SeqNo
        );
        await parameterRepository.SaveChangesAsync(cancellationToken);

        return new UpdateParameterResult(true);
    }
}