using Notification.Contracts.Email;

namespace Notification.Infrastructure.Email.Attachments;

/// <summary>
/// Resolves a single typed attachment reference into zero-or-more ready-to-send
/// <see cref="EmailAttachment"/>s. One implementation per <see cref="Type"/> discriminator.
/// </summary>
public interface IEmailAttachmentResolver
{
    /// <summary>The ref type this resolver handles (e.g. "document", "report").</summary>
    string Type { get; }

    /// <summary>
    /// Resolves the <paramref name="value"/> associated with an <see cref="EmailAttachmentRef"/>
    /// into actual attachment bytes. Returns an empty list if the resource is unavailable.
    /// </summary>
    Task<IReadOnlyList<EmailAttachment>> ResolveAsync(string value, CancellationToken ct);
}
