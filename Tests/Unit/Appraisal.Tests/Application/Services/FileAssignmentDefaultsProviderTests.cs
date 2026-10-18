using Appraisal.Application.Features.Assignments.GetAssignmentDefaults;
using Appraisal.Application.Services;
using Appraisal.Infrastructure.FileAssignmentDefaults;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Appraisal.Tests.Application.Services;

public sealed class FileAssignmentDefaultsProviderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("assignment-defaults-").FullName;
    private readonly WarningLogger<FileAssignmentDefaultsProvider> _log = new();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private FileAssignmentDefaultsProvider Provider(string? json)
    {
        if (json is not null)
        {
            Directory.CreateDirectory(Path.Combine(_root, "Config"));
            File.WriteAllText(Path.Combine(_root, "Config", "assignment-defaults.json"), json);
        }

        var env = Substitute.For<IHostEnvironment>();
        env.ContentRootPath.Returns(_root);
        return new FileAssignmentDefaultsProvider(env, _log);
    }

    [Fact]
    public void Missing_file_gives_empty_snapshot_and_only_an_info_log()
    {
        var current = Provider(null).Current;

        Assert.Empty(current.Entries);
        Assert.Equal([LogLevel.Information], _log.Levels);
    }

    [Fact]
    public void Invalid_json_gives_empty_snapshot_and_an_error_without_throwing()
    {
        var current = Provider("{ not json").Current;

        Assert.Empty(current.Entries);
        Assert.Contains(LogLevel.Error, _log.Levels);
    }

    [Fact]
    public void Json_null_gives_empty_snapshot_and_a_warning()
    {
        var current = Provider("null").Current;

        Assert.Empty(current.Entries);
        Assert.Single(_log.Warnings);
    }

    [Fact]
    public void Wrong_shape_gives_empty_snapshot_and_an_error_without_throwing()
    {
        var current = Provider("[1, 2]").Current;

        Assert.Empty(current.Entries);
        Assert.Contains(LogLevel.Error, _log.Levels);
    }

    [Fact]
    public void Valid_file_is_loaded_and_sanitized()
    {
        var current = Provider("""
            { "reappraisal": { "isActive": true, "assignmentType": "external", "assignmentMethod": "RoundRobin" },
              "New": { "isActive": false } }
            """).Current;

        var entry = Assert.Single(current.Entries).Value;
        Assert.Equal("External", entry.AssignmentType);
        Assert.Equal("RoundRobin", entry.AssignmentMethod);
    }

    [Fact]
    public void A_malformed_entry_is_skipped_by_name_and_the_others_still_load()
    {
        var current = Provider("""
            { "New": { "assignmentMehod": "Manual" },
              "Progressive": "Manual",
              "ReAppraisal": { "isActive": "yes" },
              "PreAppraisal": { "assignmentType": "Internal", "assignmentMethod": "Manual" } }
            """).Current;

        var entry = Assert.Single(current.Entries);
        Assert.Equal("PreAppraisal", entry.Key);
        Assert.Equal(3, _log.Warnings.Count);
        Assert.Contains(_log.Warnings, w => w.Contains("'New'"));
        Assert.Contains(_log.Warnings, w => w.Contains("'Progressive'"));
        Assert.Contains(_log.Warnings, w => w.Contains("'ReAppraisal'"));
    }

    [Fact]
    public void Casing_duplicates_keep_the_first_entry_in_file_order()
    {
        var current = Provider("""
            { "ReAppraisal": { "assignmentType": "External" },
              "reappraisal": { "assignmentType": "Internal" } }
            """).Current;

        Assert.Equal("External", current.Entries["ReAppraisal"].AssignmentType);
        Assert.Single(_log.Warnings);
    }

    [Fact]
    public void A_malformed_entry_still_counts_as_seen_so_a_later_casing_duplicate_is_skipped()
    {
        var current = Provider("""
            { "New": { "assignmentMehod": "Manual" },
              "new": { "assignmentType": "Internal" } }
            """).Current;

        Assert.Empty(current.Entries);
        Assert.Equal(2, _log.Warnings.Count);
        Assert.Contains(_log.Warnings, w => w.Contains("'new'") && w.Contains("duplicates"));
    }

    [Fact]
    public void A_null_entry_warns_naming_the_key()
    {
        var current = Provider("""{ "New": null }""").Current;

        Assert.Empty(current.Entries);
        Assert.Contains(_log.Warnings, w => w.Contains("'New'"));
    }

    [Fact]
    public void Comments_and_trailing_commas_inside_and_between_entries_are_tolerated()
    {
        var current = Provider("""
            {
              // between entries
              "New": {
                "assignmentType": "Internal", // inside an entry
                /* block comment */ "assignmentMethod": "Manual",
              },
              "PreAppraisal": { "assignmentType": "External", },
            }
            """).Current;

        Assert.Equal(2, current.Entries.Count);
        Assert.Equal("Manual", current.Entries["New"].AssignmentMethod);
        Assert.Equal("External", current.Entries["PreAppraisal"].AssignmentType);
        Assert.Empty(_log.Warnings);
    }

    [Fact]
    public void The_shipped_config_file_resolves_as_intended()
    {
        // Walk up from the test assembly to the repo root, then read the file the API actually deploys.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Bootstrapper", "Api", "Config", "assignment-defaults.json")))
            dir = dir.Parent;
        Assert.NotNull(dir);

        var env = Substitute.For<IHostEnvironment>();
        env.ContentRootPath.Returns(Path.Combine(dir.FullName, "Bootstrapper", "Api"));
        var snapshot = new FileAssignmentDefaultsProvider(env, _log).Current;

        var expected = new GetAssignmentDefaultsResult("External", "RoundRobin", "RoundRobin");
        Assert.Equal(expected, DefaultAssignmentResolver.Resolve(snapshot, "ReAppraisal"));
        Assert.Equal(expected, DefaultAssignmentResolver.Resolve(snapshot, "PreAppraisal"));
        Assert.Equal(AssignmentDefaultValues.Baseline, DefaultAssignmentResolver.Resolve(snapshot, "New"));
        Assert.Equal(AssignmentDefaultValues.Baseline, DefaultAssignmentResolver.Resolve(snapshot, "Progressive"));
        Assert.Empty(_log.Warnings);
    }

    [Fact]
    public void File_is_read_once_so_later_edits_are_ignored()
    {
        var provider = Provider("""{ "New": { "assignmentType": "Internal" } }""");
        var first = provider.Current;

        File.WriteAllText(Path.Combine(_root, "Config", "assignment-defaults.json"), "{}");

        Assert.Same(first, provider.Current);
        Assert.Single(provider.Current.Entries);
    }
}
