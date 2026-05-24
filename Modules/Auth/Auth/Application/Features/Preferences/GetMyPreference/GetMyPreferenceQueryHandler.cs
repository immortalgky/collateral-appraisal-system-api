using System.Text.Json;
using Auth.Domain.Preferences;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;

namespace Auth.Application.Features.Preferences.GetMyPreference;

public class GetMyPreferenceQueryHandler(AuthDbContext dbContext, ILogger<GetMyPreferenceQueryHandler> logger)
    : IQueryHandler<GetMyPreferenceQuery, GetMyPreferenceResult>
{
    public async Task<GetMyPreferenceResult> Handle(GetMyPreferenceQuery query, CancellationToken cancellationToken)
    {
        if (!UserPreferenceKeys.All.Contains(query.Key))
            throw new BadRequestException($"Unknown preference key '{query.Key}'.");

        var row = await dbContext.UserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == query.UserId && p.Key == query.Key, cancellationToken);

        if (row is null)
            return new GetMyPreferenceResult(null);

        try
        {
            using var doc = JsonDocument.Parse(row.Value);
            var element = doc.RootElement.Clone();
            return new GetMyPreferenceResult(element);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Corrupt JSON in UserPreferences for UserId={UserId} Key={Key}. Treating as not set.", query.UserId, query.Key);
            return new GetMyPreferenceResult(null);
        }
    }
}
