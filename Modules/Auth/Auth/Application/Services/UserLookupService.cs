using Auth.Contracts.Users;
using Auth.Domain.Companies;
using Auth.Domain.Identity;
using Auth.Domain.Organization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Time;

namespace Auth.Application.Services;

/// <summary>
/// Implements <see cref="IUserLookupService"/> using ASP.NET Identity's user store.
/// Returns first/last name (and company name when available) for a batch of usernames
/// in a single round-trip.
/// </summary>
public class UserLookupService(
    AuthDbContext db,
    UserManager<ApplicationUser> userManager,
    IDateTimeProvider dateTimeProvider) : IUserLookupService
{
    /// <summary>The group the seeder creates for the IntAdmin role; it monitors everyone's work.</summary>
    private const string IntAdminGroupName = "IntAdmin";

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
                    company != null ? company.Name : null,
                    company != null ? company.NameLocal : null))
            .ToListAsync(cancellationToken);

        return users.ToDictionary(u => u.Username, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<string[]> GetUsernamesInRoleAsync(
        string role,
        Guid? companyId = null,
        CancellationToken ct = default)
    {
        var usersInRole = await userManager.GetUsersInRoleAsync(role);

        return usersInRole
            .Where(u => u.IsActive
                        && u.UserName != null
                        && (companyId == null || u.CompanyId == companyId))
            .Select(u => u.UserName!)
            .ToArray();
    }

    public async Task<IReadOnlyList<string>> GetEmailsInRoleAsync(string role, CancellationToken ct)
    {
        var normalizedRole = role.ToUpperInvariant();
        return await EmailsOf(UsableWithEmail().Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id
            && db.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == normalizedRole))), ct);
    }

    public async Task<IReadOnlyList<string>> GetEmailsAsync(IEnumerable<string> usernames, CancellationToken ct)
    {
        var normalized = usernames.Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u.Trim().ToUpperInvariant()).Distinct().ToArray();
        if (normalized.Length == 0) return [];

        return await EmailsOf(UsableWithEmail().Where(u => normalized.Contains(u.NormalizedUserName)), ct);
    }

    public async Task<IReadOnlyList<string>> GetSupervisorEmailsAsync(string username, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username)) return [];
        var normalized = username.Trim().ToUpperInvariant();

        var usersGroups = db.GroupUsers
            .Where(gu => db.Users.Any(u => u.Id == gu.UserId && u.NormalizedUserName == normalized))
            .Select(gu => gu.GroupId);
        // Joined through Groups so its soft-delete filter applies to both sides.
        var monitorGroups =
            from gm in db.GroupMonitoring
            join monitor in db.Groups on gm.MonitorGroupId equals monitor.Id
            join monitored in db.Groups on gm.MonitoredGroupId equals monitored.Id
            where usersGroups.Contains(monitored.Id) && monitor.Name != IntAdminGroupName
            select monitor.Id;

        return await EmailsOf(UsableWithEmail()
            .Where(u => db.GroupUsers.Any(gu => gu.UserId == u.Id && monitorGroups.Contains(gu.GroupId))), ct);
    }

    /// <summary>Users who may use the system now (<see cref="ApplicationUser.IsUsable(DateTime)"/>) and have an email.</summary>
    private IQueryable<ApplicationUser> UsableWithEmail()
    {
        var now = dateTimeProvider.ApplicationNow;
        return db.Users.Where(u => u.IsActive
                                   && (u.AccessExpiresAt == null || u.AccessExpiresAt > now)
                                   && u.Email != null && u.Email.Trim() != "");
    }

    private static async Task<IReadOnlyList<string>> EmailsOf(IQueryable<ApplicationUser> users, CancellationToken ct)
    {
        var emails = await users.Select(u => u.Email!.Trim()).ToListAsync(ct);
        return emails.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<RequestorInfoDto?> GetRequestorAsync(string employeeId, CancellationToken ct = default)
    {
        var user = await db.Set<ApplicationUser>()
            .Where(u => u.UserName == employeeId && u.IsActive)
            .FirstOrDefaultAsync(ct);

        if (user is null) return null;

        // Left-join Officer on AoCode (AoCode == OfficerCode)
        Officer? officer = null;
        if (user.AoCode != null)
            officer = await db.Set<Officer>()
                .Where(o => o.OfficerCode == user.AoCode)
                .FirstOrDefaultAsync(ct);

        // Left-join CostCenter on Officer.CostCenterCode.
        // There is a known 8-digit vs 3-digit mismatch; a null result here is expected.
        string? costCenterDesc = null;
        if (officer?.CostCenterCode != null)
            costCenterDesc = await db.Set<CostCenter>()
                .Where(c => c.Code == officer.CostCenterCode)
                .Select(c => c.Description)
                .FirstOrDefaultAsync(ct);

        return new RequestorInfoDto(
            UserId: user.Id,
            EmployeeId: user.UserName ?? "",
            Name: $"{user.FirstName} {user.LastName}".Trim(),
            Email: user.Email,
            ContactNo: user.PhoneNumber,
            AoCode: user.AoCode,
            CostCenterCode: officer?.CostCenterCode,
            CostCenterDescription: costCenterDesc,
            Department: user.Department);
    }
}
