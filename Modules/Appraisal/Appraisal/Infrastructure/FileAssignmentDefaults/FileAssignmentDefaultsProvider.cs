using System.Text.Json;
using System.Text.Json.Serialization;
using Appraisal.Application.Features.Assignments.GetAssignmentDefaults;
using Appraisal.Application.Services;
using Microsoft.Extensions.Hosting;

namespace Appraisal.Infrastructure.FileAssignmentDefaults;

/// <summary>
/// Reads Config/assignment-defaults.json from the content root once; UseAppraisalModule triggers the load at startup. The repo owns the
/// file: edits ship via deploy and take effect on the next start. A missing or unusable file falls back
/// to the built-in defaults; nothing escapes <see cref="Current"/>.
/// </summary>
public sealed class FileAssignmentDefaultsProvider : IAssignmentDefaultsProvider
{
    // Disallow: a misspelled field ("assignmentMehod") fails its entry instead of being ignored silently.
    private static readonly JsonSerializerOptions EntryOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        // Deserialize re-reads each entry's raw text, so the document's comment/comma leniency must repeat here.
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly string _path;
    private readonly ILogger<FileAssignmentDefaultsProvider> _logger;
    private readonly Lazy<AssignmentDefaultsSnapshot> _snapshot;

    public FileAssignmentDefaultsProvider(IHostEnvironment env, ILogger<FileAssignmentDefaultsProvider> logger)
    {
        _path = Path.Combine(env.ContentRootPath, "Config", "assignment-defaults.json");
        _logger = logger;
        _snapshot = new Lazy<AssignmentDefaultsSnapshot>(Load);
    }

    public AssignmentDefaultsSnapshot Current => _snapshot.Value;

    private AssignmentDefaultsSnapshot Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                _logger.LogInformation("Assignment defaults file not found at {Path}; using built-in defaults", _path);
                return AssignmentDefaultsSnapshot.Empty;
            }

            using var stream = File.OpenRead(_path);
            using var document = JsonDocument.Parse(stream, DocumentOptions);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Null)
            {
                _logger.LogWarning("Assignment defaults file {Path} is empty (null); using built-in defaults", _path);
                return AssignmentDefaultsSnapshot.Empty;
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                _logger.LogError("Assignment defaults file {Path} must be a JSON object; using built-in defaults", _path);
                return AssignmentDefaultsSnapshot.Empty;
            }

            // Document order, one entry at a time: a bad entry is skipped without taking the others down.
            // It is still passed on as null, so a later casing-duplicate of its key is reported, not promoted.
            var raw = new List<KeyValuePair<string, AssignmentDefaultsEntry?>>();
            foreach (var property in root.EnumerateObject())
            {
                AssignmentDefaultsEntry? entry = null;
                try
                {
                    entry = property.Value.Deserialize<AssignmentDefaultsEntry>(EntryOptions);
                    if (entry is null)
                        _logger.LogWarning("Assignment defaults: entry '{Key}' is null and was skipped", property.Name);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(
                        ex, "Assignment defaults: entry '{Key}' is malformed and was skipped", property.Name);
                }

                raw.Add(new(property.Name, entry));
            }

            var snapshot = DefaultAssignmentValidator.Sanitize(raw, _logger);
            _logger.LogInformation(
                "Assignment defaults loaded from {Path}: {Active} active of {Total} entries",
                _path, snapshot.Entries.Count, raw.Count);
            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Assignment defaults file {Path} could not be loaded; using built-in defaults", _path);
            return AssignmentDefaultsSnapshot.Empty;
        }
    }
}
