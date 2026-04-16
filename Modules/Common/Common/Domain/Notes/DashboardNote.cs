namespace Common.Domain.Notes;

/// <summary>
/// Personal note scoped to a single user on their dashboard.
/// No sharing, no entity linking, plain text only.
/// </summary>
public class DashboardNote
{
    public const int MaxContentLength = 10_000;

    public Guid Id { get; private set; }

    /// <summary>
    /// The user who owns this note (from the "sub" JWT claim).
    /// Scoping enforced in all handlers — foreign users receive 404, not 403.
    /// </summary>
    public Guid UserId { get; private set; }

    public string Content { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    // Required by EF Core
    private DashboardNote()
    {
    }

    /// <summary>
    /// Factory — creates a new personal note for the given user.
    /// </summary>
    /// <param name="userId">The authenticated user's Id (from ICurrentUserService.UserId).</param>
    /// <param name="content">Non-empty plain-text content (max 10,000 characters).</param>
    public static DashboardNote Create(Guid userId, string content)
    {
        ValidateContent(content);

        var now = DateTimeOffset.UtcNow;
        return new DashboardNote
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Content = content.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Replaces the note content and bumps <see cref="UpdatedAt"/>.
    /// </summary>
    public void UpdateContent(string content)
    {
        ValidateContent(content);
        Content = content.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void ValidateContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Note content cannot be empty or whitespace.", nameof(content));

        if (content.Length > MaxContentLength)
            throw new ArgumentException(
                $"Note content exceeds the maximum allowed length of {MaxContentLength} characters.",
                nameof(content));
    }
}
