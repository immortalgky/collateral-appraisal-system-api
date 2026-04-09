using Auth.Contracts.Users;
using Auth.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Auth.Application.Services;

/// <summary>
/// Implements <see cref="IUserLookupService"/> using ASP.NET Identity's user store.
/// Returns first/last name for a batch of usernames in a single round-trip.
/// </summary>
public class UserLookupService(UserManager<ApplicationUser> userManager) : IUserLookupService
{
    public async Task<IReadOnlyDictionary<string, UserLookupDto>> GetByUsernamesAsync(
        IEnumerable<string> usernames,
        CancellationToken cancellationToken)
    {
        var list = usernames
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (list.Length == 0)
            return new Dictionary<string, UserLookupDto>(StringComparer.OrdinalIgnoreCase);

        var users = await userManager.Users
            .Where(u => u.UserName != null && list.Contains(u.UserName))
            .Select(u => new UserLookupDto(u.UserName!, u.FirstName, u.LastName))
            .ToListAsync(cancellationToken);

        return users.ToDictionary(u => u.Username, StringComparer.OrdinalIgnoreCase);
    }
}
