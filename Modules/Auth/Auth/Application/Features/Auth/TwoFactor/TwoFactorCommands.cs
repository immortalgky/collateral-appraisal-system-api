using Auth.Application.Services;

namespace Auth.Application.Features.Auth.TwoFactor;

public record BeginTwoFactorSetupCommand(Guid UserId) : ICommand<TwoFactorSetupInfo>;

public record EnableTwoFactorCommand(Guid UserId, string? Code) : ICommand<RecoveryCodesResult>;

public record DisableTwoFactorCommand(Guid UserId, string? Code) : ICommand;

public record RegenerateRecoveryCodesCommand(Guid UserId, string? Code) : ICommand<RecoveryCodesResult>;

/// <summary>The one-time recovery codes. Shown once and never readable again.</summary>
public record RecoveryCodesResult(IReadOnlyList<string> RecoveryCodes);

/// <summary>Body of the endpoints that need a current authenticator code.</summary>
public record TwoFactorCodeRequest(string? Code);
