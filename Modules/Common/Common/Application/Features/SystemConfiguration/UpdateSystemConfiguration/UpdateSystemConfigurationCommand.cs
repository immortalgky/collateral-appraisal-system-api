using MediatR;
using Shared.CQRS;

namespace Common.Application.Features.SystemConfiguration.UpdateSystemConfiguration;

public record UpdateSystemConfigurationCommand(
    string Key,
    string Value,
    string? Description,
    bool? IsActive) : ICommand<Unit>;
