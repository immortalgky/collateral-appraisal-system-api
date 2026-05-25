using System.Text.Json;

namespace Auth.Application.Features.Preferences.SetMyPreference;

public record SetMyPreferenceCommand(Guid UserId, string Key, JsonElement Value) : ICommand;
