using System.Text.Json;
using Auth.Domain.Preferences;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;

namespace Auth.Application.Features.Preferences.SetMyPreference;

public class SetMyPreferenceCommandHandler(AuthDbContext dbContext)
    : ICommandHandler<SetMyPreferenceCommand>
{
    private static readonly IReadOnlySet<int> PkViolationNumbers = new HashSet<int> { 2627, 2601 };

    public async Task<Unit> Handle(SetMyPreferenceCommand command, CancellationToken cancellationToken)
    {
        if (!UserPreferenceKeys.All.Contains(command.Key))
            throw new BadRequestException($"Unknown preference key '{command.Key}'.");

        var json = JsonSerializer.Serialize(command.Value);

        if (json.Length > 64 * 1024)
            throw new BadRequestException("Preference value too large.");

        var existing = await dbContext.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == command.UserId && p.Key == command.Key, cancellationToken);

        if (existing is not null)
        {
            existing.Update(json);
        }
        else
        {
            var preference = UserPreference.Create(command.UserId, command.Key, json);
            dbContext.UserPreferences.Add(preference);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: var n } && PkViolationNumbers.Contains(n))
        {
            // Two concurrent PUTs for the same (UserId, Key) both passed the null-check — second INSERT hit the PK.
            // Re-load the now-existing row and update it.
            dbContext.ChangeTracker.Clear();

            var raced = await dbContext.UserPreferences
                .FirstOrDefaultAsync(p => p.UserId == command.UserId && p.Key == command.Key, cancellationToken);

            if (raced is not null)
                raced.Update(json);

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Unit.Value;
    }
}
