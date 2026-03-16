namespace Auth.Application.Dtos;

public record RegisterClientDto(
    string DisplayName,
    string ClientType,
    List<Uri> PostLogoutRedirectUris,
    List<Uri> RedirectUris,
    List<string> Permissions,
    List<string> Requirements
);
