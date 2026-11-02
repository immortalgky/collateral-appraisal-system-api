using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Identity;

namespace Auth.Application.Features.Users.GetUserByUsername;

public record GetUserByUsernameQuery(string Username) : IQuery<UserContactDto>;

/// <summary>
/// The contact card of a user, for any signed-in user (a person picked by employee code; bank staff only: system and company accounts
/// are not found). Contact details only, phone included because the card is for bank staff. No roles, permissions, groups or teams: those are the admin detail, GET /auth/users/{id}.
/// </summary>
public record UserContactDto(
    Guid Id,
    string Username,
    string FirstName,
    string LastName,
    string? Email,
    string? PhoneNumber,
    string? Position,
    string? Department,
    string? AvatarUrl);

public class GetUserByUsernameQueryHandler(UserManager<ApplicationUser> userManager, ICurrentUserService currentUser)
    : IQueryHandler<GetUserByUsernameQuery, UserContactDto>
{
    public async Task<UserContactDto> Handle(GetUserByUsernameQuery query, CancellationToken cancellationToken)
    {
        // The card shows bank staff to bank staff: a company user may not look anyone up, and company users are not found.
        if (currentUser.IsExternal)
            throw new ForbiddenException("Company users cannot look up bank staff.");

        // Identity's own normalizer; system accounts are hidden as in the user list. Inactive users are returned: this
        // feeds a card for an already-chosen person, not a picker.
        var normalized = userManager.NormalizeName(query.Username.Trim());
        return await userManager.Users.AsNoTracking()
                   .Where(u => u.NormalizedUserName == normalized && !u.IsSystem && u.CompanyId == null)
                   .Select(u => new UserContactDto(
                       u.Id, u.UserName ?? "", u.FirstName, u.LastName, u.Email, u.PhoneNumber,
                       u.Position, u.Department, u.AvatarUrl))
                   .FirstOrDefaultAsync(cancellationToken)
               ?? throw new NotFoundException("User", query.Username);
    }
}
