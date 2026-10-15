using Shared.CQRS;

namespace Common.Application.Features.Logs.GetLogById;

public record GetLogByIdQuery(long Id) : IQuery<LogDetailDto>;
