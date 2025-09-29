namespace Auth.Auth.Features.RegisterClient;

public record RegisterClientRequest(
    string DisplayName,
    string ClientType,
    List<Uri> PostLogoutRedirectUris,
    List<Uri> RedirectUris,
    List<string> Permissions,
    List<string> Requirements
);
