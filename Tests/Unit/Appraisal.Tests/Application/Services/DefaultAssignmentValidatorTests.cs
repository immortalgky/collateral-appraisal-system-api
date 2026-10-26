using Appraisal.Application.Features.Assignments.GetAssignmentDefaults;
using Appraisal.Application.Services;
using Microsoft.Extensions.Logging;

namespace Appraisal.Tests.Application.Services;

public class DefaultAssignmentValidatorTests
{
    private static AssignmentDefaultsEntry Entry(
        string? type = null, string? method = null, string? followup = null, bool active = true) =>
        new(active, type, method, followup);

    private static (AssignmentDefaultsSnapshot Snapshot, WarningLogger Log) Sanitize(
        params (string Key, AssignmentDefaultsEntry? Entry)[] items)
    {
        var log = new WarningLogger();
        var raw = items.Select(i => new KeyValuePair<string, AssignmentDefaultsEntry?>(i.Key, i.Entry));
        return (DefaultAssignmentValidator.Sanitize(raw, log), log);
    }

    [Fact]
    public void Casing_is_normalized_to_the_canonical_spelling()
    {
        var (snapshot, log) = Sanitize(("reappraisal", Entry("external", "ROUNDROBIN", "manual")));

        var entry = snapshot.Entries["ReAppraisal"];
        Assert.Equal("External", entry.AssignmentType);
        Assert.Equal("RoundRobin", entry.AssignmentMethod);
        Assert.Equal("Manual", entry.InternalFollowupAssignmentMethod);
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void Invalid_value_is_skipped_with_a_warning_and_the_other_fields_survive()
    {
        var (snapshot, log) = Sanitize(("New", Entry("Sideways", "Manual")));

        var entry = snapshot.Entries["New"];
        Assert.Null(entry.AssignmentType);
        Assert.Equal("Manual", entry.AssignmentMethod);
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void Inactive_entry_is_dropped()
    {
        var (snapshot, _) = Sanitize(("New", Entry("External", active: false)));

        Assert.Empty(snapshot.Entries);
    }

    [Fact]
    public void Unknown_appraisal_type_is_skipped_with_a_warning()
    {
        var (snapshot, log) = Sanitize(("Bogus", Entry("External")));

        Assert.Empty(snapshot.Entries);
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void Keys_differing_only_by_casing_keep_the_first_and_warn()
    {
        var (snapshot, log) = Sanitize(
            ("ReAppraisal", Entry("External", "Manual")),
            ("reappraisal", Entry("Internal", "RoundRobin")));

        Assert.Equal("External", snapshot.Entries["ReAppraisal"].AssignmentType);
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void Key_with_surrounding_whitespace_is_matched_and_counts_as_a_duplicate()
    {
        var (snapshot, log) = Sanitize(
            (" New ", Entry("External")),
            ("new", Entry("Internal")));

        Assert.Equal("External", snapshot.Entries["New"].AssignmentType);
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void Exact_duplicate_key_keeps_the_first_in_file_order_and_warns()
    {
        var (snapshot, log) = Sanitize(("New", Entry("External")), ("New", Entry("Internal")));

        Assert.Equal("External", snapshot.Entries["New"].AssignmentType);
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void Inactive_duplicate_still_warns()
    {
        var (snapshot, log) = Sanitize(
            ("ReAppraisal", Entry("External")),
            ("reappraisal", Entry("Internal", active: false)));

        Assert.Equal("External", snapshot.Entries["ReAppraisal"].AssignmentType);
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void Internal_with_quotation_warns_but_is_kept_for_the_resolver_to_downgrade()
    {
        var (snapshot, log) = Sanitize(("New", Entry("Internal", "Quotation")));

        Assert.Equal("Quotation", snapshot.Entries["New"].AssignmentMethod);
        Assert.Single(log.Warnings);
    }
}

/// <summary>Collects warning-level messages so tests can assert that a problem was reported.</summary>
internal sealed class WarningLogger : ILogger
{
    public List<string> Warnings { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (logLevel == LogLevel.Warning)
            Warnings.Add(formatter(state, exception));
    }
}

internal sealed class WarningLogger<T> : ILogger<T>
{
    private readonly WarningLogger _inner = new();
    public List<string> Warnings => _inner.Warnings;
    public List<LogLevel> Levels { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Levels.Add(logLevel);
        _inner.Log(logLevel, eventId, state, exception, formatter);
    }
}
