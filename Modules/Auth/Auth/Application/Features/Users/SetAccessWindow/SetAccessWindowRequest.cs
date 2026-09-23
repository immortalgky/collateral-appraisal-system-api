namespace Auth.Application.Features.Users.SetAccessWindow;

public record SetAccessWindowRequest(DateTime ExpiresAt, string Reason, bool ExtendOnly = false);
