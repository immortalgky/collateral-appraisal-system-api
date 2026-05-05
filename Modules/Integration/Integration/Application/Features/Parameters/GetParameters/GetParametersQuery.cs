using Shared.CQRS;

namespace Integration.Application.Features.Parameters.GetParameters;

public record GetParametersQuery(IReadOnlyCollection<string>? Groups) : IQuery<List<ParameterGroupResult>>;

public record ParameterGroupResult(string Group, List<ParameterValueItem> Values);
public record ParameterValueItem(string Code, string Description);
