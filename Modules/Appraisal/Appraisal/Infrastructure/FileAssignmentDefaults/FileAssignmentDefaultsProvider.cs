using System.Text.Json;
using Appraisal.Application.Features.Assignments.GetAssignmentDefaults;
using Appraisal.Application.Services;
using Microsoft.Extensions.Hosting;

namespace Appraisal.Infrastructure.FileAssignmentDefaults;

/// <summary>
/// Reads Config/assignment-defaults.json from the content root. The file's last-write time is checked
/// on every read, so an edit applies on the next request without a restart. A bad edit is logged and
/// the last good values are kept.
/// </summary>
public sealed class FileAssignmentDefaultsProvider(
    IHostEnvironment env,
    ILogger<FileAssignmentDefaultsProvider> logger) : IAssignmentDefaultsProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly string _path = Path.Combine(env.ContentRootPath, "Config", "assignment-defaults.json");
    private readonly object _lock = new();
    private AssignmentDefaultsSnapshot _current = AssignmentDefaultsSnapshot.Empty;
    private DateTime? _loadedWriteTime;

    public AssignmentDefaultsSnapshot Current
    {
        get
        {
            var file = new FileInfo(_path);
            DateTime? writeTime = file.Exists ? file.LastWriteTimeUtc : null;

            lock (_lock)
            {
                if (writeTime != _loadedWriteTime)
                    Load(writeTime);
                return _current;
            }
        }
    }

    private void Load(DateTime? writeTime)
    {
        if (writeTime is null)
        {
            _current = AssignmentDefaultsSnapshot.Empty;
            _loadedWriteTime = null;
            logger.LogInformation("Assignment defaults file not found at {Path}; using built-in defaults", _path);
            return;
        }

        try
        {
            using var stream = new FileStream(
                _path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var raw = JsonSerializer.Deserialize<Dictionary<string, AssignmentDefaultsEntry?>>(stream, JsonOptions);
            if (raw is not null)
                _current = DefaultAssignmentValidator.Sanitize(raw, logger);

            logger.LogInformation(
                "Assignment defaults loaded from {Path}: {Count} appraisal type(s)", _path, _current.Entries.Count);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Assignment defaults file is not valid JSON; keeping the previous values");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not marked as loaded, so the next request tries again.
            logger.LogWarning(ex, "Assignment defaults file could not be read; keeping the previous values");
            return;
        }

        _loadedWriteTime = writeTime;
    }
}
