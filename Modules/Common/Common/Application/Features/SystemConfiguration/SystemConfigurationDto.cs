namespace Common.Application.Features.SystemConfiguration;

public record SystemConfigurationDto(
    Guid Id,
    string Key,
    string Value,
    string ValueType,
    string? Description,
    string? Category,
    bool IsActive);
