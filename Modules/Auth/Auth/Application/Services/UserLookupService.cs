using Auth.Contracts.Users;
using Auth.Domain.Companies;
using Auth.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Auth.Application.Services;

/// <summary>
/// Implements <see cref="IUserLookupService"/> using ASP.NET Identity's user store.
/// Returns first/last name (and company name when available) for a batch of usernames
/// in a single round-trip.
/// </summary>
public class UserLookupService(
    UserManager<ApplicationUser> userManager,
    AuthDbContext db) : IUserLookupService
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

        var users = await db.Set<ApplicationUser>()
            .Where(u => u.UserName != null && list.Contains(u.UserName))
            .GroupJoin(
                db.Set<Company>().Where(c => !c.IsDeleted),
                u => u.CompanyId,
                c => c.Id,
                (u, companies) => new { User = u, Companies = companies })
            .SelectMany(
                x => x.Companies.DefaultIfEmpty(),
                (x, company) => new UserLookupDto(
                    x.User.UserName!,
                    x.User.FirstName,
                    x.User.LastName,
                    company != null ? company.Name : null))
            .ToListAsync(cancellationToken);

        return users.ToDictionary(u => u.Username, StringComparer.OrdinalIgnoreCase);
    }
}
