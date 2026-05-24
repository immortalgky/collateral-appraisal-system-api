using System.Text.Json;

namespace Auth.Application.Features.Preferences.GetMyPreference;

public record GetMyPreferenceQuery(Guid UserId, string Key) : IQuery<GetMyPreferenceResult>;

public record GetMyPreferenceResult(JsonElement? Value);
