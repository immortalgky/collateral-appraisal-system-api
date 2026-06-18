namespace Parameter.Parameters.Features.GetParameter;

internal class GetParametersQueryHandler(IParameterRepository parameterRepository) : IQueryHandler<GetParametersQuery, GetParametersResult>
{
    public async Task<GetParametersResult> Handle(GetParametersQuery query, CancellationToken cancellationToken)
    {
        var parameter = await parameterRepository.GetParameter(query.Parameter, true, cancellationToken);

        var result = parameter.Select(p => new ParameterDto(
            ParId: p.Id,
            Group: p.Group,
            Country: p.Country,
            Language: p.Language,
            Code: p.Code,
            Description: p.Description,
            IsActive: p.IsActive,
            SeqNo: p.SeqNo
        )).ToList();

        return new GetParametersResult(result);
    }
}
