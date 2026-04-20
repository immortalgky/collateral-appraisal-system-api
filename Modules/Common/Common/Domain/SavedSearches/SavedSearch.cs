namespace Common.Domain.SavedSearches;

/// <summary>
/// A named snapshot of a user's search filter configuration.
/// Scoped strictly to the owning user — all handlers enforce UserId filtering.
/// The FiltersJson payload is stored opaquely; no structural validation is applied.
/// </summary>
public class SavedSearch
{
    public const int MaxNameLength = 100;
    public const int MaxEntityTypeLength = 50;
    public const int MaxSortByLength = 50;
    public const int MaxSortDirLength = 10;
    public const int MaxFiltersJsonLength = 8_000;
    public const int MaxPerUser = 50;

    public Guid Id { get; private set; }

    /// <summary>
    /// The authenticated user who owns this saved search (from the "sub" JWT claim).
    /// Scoping enforced in all handlers — foreign users receive 404, not 403.
    /// </summary>
    public Guid UserId { get; private set; }

    public string Name { get; private set; } = null!;

    /// <summary>
    /// The entity kind this search applies to, e.g. "appraisal", "request".
    /// Stored in lower-invariant form.
    /// </summary>
    public string EntityType { get; private set; } = null!;

    /// <summary>
    /// Opaque JSON blob of the filter parameters. Structure is owned by the client.
    /// </summary>
    public string FiltersJson { get; private set; } = null!;

    public string? SortBy { get; private set; }

    public string? SortDir { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    // Required by EF Core
    private SavedSearch()
    {
    }

    /// <summary>
    /// Factory — creates a new saved search for the given user.
    /// </summary>
    /// <param name="userId">The authenticated user's Id (from ICurrentUserService.UserId).</param>
    /// <param name="name">Non-empty display name (max 100 characters).</param>
    /// <param name="entityType">Non-empty entity kind, e.g. "appraisal" (stored lower-invariant).</param>
    /// <param name="filtersJson">Non-empty JSON string representing the filter state.</param>
    /// <param name="sortBy">Optional sort column name (max 50 characters).</param>
    /// <param name="sortDir">Optional sort direction, e.g. "asc" / "desc" (max 10 characters).</param>
    public static SavedSearch Create(
        Guid userId,
        string name,
        string entityType,
        string filtersJson,
        string? sortBy,
        string? sortDir)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Saved search name cannot be empty.", nameof(name));

        if (name.Trim().Length > MaxNameLength)
            throw new ArgumentException(
                $"Saved search name exceeds the maximum allowed length of {MaxNameLength} characters.",
                nameof(name));

        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("Entity type cannot be empty.", nameof(entityType));

        if (string.IsNullOrWhiteSpace(filtersJson))
            throw new ArgumentException("FiltersJson cannot be empty.", nameof(filtersJson));

        if (filtersJson.Length > MaxFiltersJsonLength)
            throw new ArgumentException(
                $"FiltersJson exceeds the maximum allowed length of {MaxFiltersJsonLength} characters.",
                nameof(filtersJson));

        var now = DateTimeOffset.Now;
        return new SavedSearch
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = name.Trim(),
            EntityType = entityType.Trim().ToLowerInvariant(),
            FiltersJson = filtersJson,
            SortBy = sortBy,
            SortDir = sortDir,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
