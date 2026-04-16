namespace Auth.Contracts.Users;

public record UserLookupDto(string Username, string FirstName, string LastName, string? CompanyName = null);
