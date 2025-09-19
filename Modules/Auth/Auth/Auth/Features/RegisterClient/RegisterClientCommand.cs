namespace Auth.Auth.Features.RegisterClient;

public record RegisterClientCommand(
    string DisplayName,
    string ClientType,
    List<Uri> PostLogoutRedirectUris,
    List<Uri> RedirectUris,
    List<string> Permissions,
    List<string> Requirements
) : ICommand<RegisterClientResult>;
