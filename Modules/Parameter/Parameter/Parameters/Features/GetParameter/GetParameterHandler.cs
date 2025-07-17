using Mapster;
using Microsoft.IdentityModel.Tokens;
using Parameter.Parameters.Exceptions;


namespace Parameter.Parameters.Features.GetParameter;

internal class GetParameterHandler(IParameterRepository parameterRepository) : IQueryHandler<GetParameterQuery, GetParameterResult>
{
    public async Task<GetParameterResult> Handle(GetParameterQuery query, CancellationToken cancellationToken)
    {
        var filter = query.Parameter.Adapt<ParameterDto>();

        var parameter = await parameterRepository.GetParameter(filter, false, cancellationToken);

        if (parameter.Count == 0) throw new ParameterNotFoundException(filter);

        var result = parameter.Adapt<List<ParameterDto>>();

        return new GetParameterResult(result);
    }
}