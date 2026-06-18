public class DeleteParameterCommandHandler(
    IParameterRepository parameterRepository
) : ICommandHandler<DeleteParameterCommand, DeleteParameterResult>
{
    public async Task<DeleteParameterResult> Handle(DeleteParameterCommand command, CancellationToken cancellationToken)
    {
        var parameter = await parameterRepository.GetParameterByParId(command.parId, cancellationToken);
        if (parameter is null) throw new NotFoundException("Parameter Not Found: ", command.parId);

        await parameterRepository.DeleteAsync(parameter.Id);
        await parameterRepository.SaveChangesAsync(cancellationToken);
        return new DeleteParameterResult(true);
    }
}