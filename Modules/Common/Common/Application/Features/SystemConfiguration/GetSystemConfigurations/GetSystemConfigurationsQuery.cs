using Shared.CQRS;

namespace Common.Application.Features.SystemConfiguration.GetSystemConfigurations;

public record GetSystemConfigurationsQuery : IQuery<List<SystemConfigurationDto>>;
