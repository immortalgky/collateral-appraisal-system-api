namespace Auth.Contracts.Users;

// CompanyNameLocal is the Thai company name (null when absent). UI callers pick by their own locale;
// the notification/email handlers prefer it outright because those messages are Thai-language.
public record UserLookupDto(string Username, string FirstName, string LastName, string? CompanyName = null, string? CompanyNameLocal = null);
