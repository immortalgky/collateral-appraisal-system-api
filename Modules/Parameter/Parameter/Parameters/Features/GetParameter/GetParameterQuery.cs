namespace Parameter.Parameters.Features.GetParameter;

public record GetParametersQuery(ParameterDto Parameter) : IQuery<GetParametersResult>;
