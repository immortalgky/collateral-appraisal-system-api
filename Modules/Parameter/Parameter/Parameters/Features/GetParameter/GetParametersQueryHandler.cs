namespace Parameter.Parameters.Features.GetParameter;

internal class GetParametersQueryHandler(IParameterRepository parameterRepository) : IQueryHandler<GetParametersQuery, GetParametersResult>
{
    public async Task<GetParametersResult> Handle(GetParametersQuery query, CancellationToken cancellationToken)
    {
        var parameter = await parameterRepository.GetParameter(query.Parameter, true, cancellationToken);

        var result = parameter.Adapt<List<ParameterDto>>();

        return new GetParametersResult(result);
    }
}
