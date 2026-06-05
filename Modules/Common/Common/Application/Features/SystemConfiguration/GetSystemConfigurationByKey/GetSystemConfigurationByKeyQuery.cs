using Shared.CQRS;

namespace Common.Application.Features.SystemConfiguration.GetSystemConfigurationByKey;

public record GetSystemConfigurationByKeyQuery(string Key) : IQuery<SystemConfigurationDto?>;
