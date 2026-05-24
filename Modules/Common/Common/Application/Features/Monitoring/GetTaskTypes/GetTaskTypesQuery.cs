using Shared.CQRS;

namespace Common.Application.Features.Monitoring.GetTaskTypes;

public record GetTaskTypesQuery : IQuery<IReadOnlyList<TaskTypeOption>>;
