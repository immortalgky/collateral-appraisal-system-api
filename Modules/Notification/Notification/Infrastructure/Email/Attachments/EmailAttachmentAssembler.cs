using Notification.Contracts.Email;

namespace Notification.Infrastructure.Email.Attachments;

/// <summary>
/// Fans out a list of <see cref="EmailAttachmentRef"/>s to the matching
/// <see cref="IEmailAttachmentResolver"/> implementations and concatenates the results.
/// Unknown <c>Type</c> values are logged and skipped — they do not abort the whole email.
/// </summary>
public sealed class EmailAttachmentAssembler
{
    private readonly IReadOnlyDictionary<string, IEmailAttachmentResolver> _resolvers;
    private readonly ILogger<EmailAttachmentAssembler> _logger;

    public EmailAttachmentAssembler(
        IEnumerable<IEmailAttachmentResolver> resolvers,
        ILogger<EmailAttachmentAssembler> logger)
    {
        _resolvers = resolvers.ToDictionary(r => r.Type, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    /// <summary>
    /// Resolves all <paramref name="refs"/> and returns the concatenated attachments.
    /// </summary>
    public async Task<IReadOnlyList<EmailAttachment>> AssembleAsync(
        IEnumerable<EmailAttachmentRef> refs,
        CancellationToken ct)
    {
        var results = new List<EmailAttachment>();

        foreach (var r in refs)
        {
            if (!_resolvers.TryGetValue(r.Type, out var resolver))
            {
                _logger.LogWarning(
                    "No email attachment resolver registered for type '{Type}' (value='{Value}'). Skipping.",
                    r.Type, r.Value);
                continue;
            }

            try
            {
                var attachments = await resolver.ResolveAsync(r.Value, ct);
                results.AddRange(attachments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error resolving email attachment type='{Type}' value='{Value}'. Skipping.",
                    r.Type, r.Value);
            }
        }

        return results;
    }
}
