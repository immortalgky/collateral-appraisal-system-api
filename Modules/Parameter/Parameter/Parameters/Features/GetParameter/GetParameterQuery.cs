namespace Parameter.Parameters.Features.GetParameter;

public record GetParameterQuery(ParameterDto Parameter) : IQuery<GetParameterResult>;