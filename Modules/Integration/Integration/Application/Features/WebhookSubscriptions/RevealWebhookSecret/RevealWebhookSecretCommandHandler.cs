using FluentValidation;
using Integration.Domain.WebhookSubscriptions;
using Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shared.CQRS;
using Shared.Exceptions;
using Shared.Identity;
using Shared.Security;
using Shared.Time;

namespace Integration.Application.Features.WebhookSubscriptions.RevealWebhookSecret;

public record RevealWebhookSecretCommand(Guid Id, string Field, string? IpAddress)
    : ICommand<RevealWebhookSecretResult>;

public record RevealWebhookSecretResult(string Value);

public class RevealWebhookSecretCommandValidator : AbstractValidator<RevealWebhookSecretCommand>
{
    public RevealWebhookSecretCommandValidator()
    {
        RuleFor(x => x.Field)
            .Must(f => f is WebhookSecretField.SecretKey or WebhookSecretField.ClientSecret)
            .WithMessage("Field must be SecretKey or ClientSecret.");
    }
}

/// <summary>
/// Decrypts one stored secret for an admin. Order matters: decrypt first (a failure leaves no audit
/// row claiming a disclosure that never happened), then save the audit row, and only then return the
/// value — if the audit cannot be written, the caller gets an error and no secret.
/// </summary>
public class RevealWebhookSecretCommandHandler(
    IntegrationDbContext dbContext,
    ColumnSecretCipher cipher,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<RevealWebhookSecretCommand, RevealWebhookSecretResult>
{
    public async Task<RevealWebhookSecretResult> Handle(
        RevealWebhookSecretCommand command,
        CancellationToken cancellationToken)
    {
        var subscription = await dbContext.WebhookSubscriptions.AsNoTracking()
                               .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
                           ?? throw new NotFoundException("WebhookSubscription", command.Id);

        var stored = command.Field == WebhookSecretField.SecretKey
            ? subscription.SecretKey
            : subscription.ClientSecret;
        if (string.IsNullOrWhiteSpace(stored))
            throw new NotFoundException($"{command.Field} is not set on webhook subscription {command.Id}.");

        // The endpoint requires two permission policies, so this is unreachable in practice; refuse
        // rather than write an audit row nobody can be held to.
        var revealedBy = currentUser.UserCode
                         ?? throw new UnauthorizedAccessException("A signed-in user is required to reveal a secret.");

        var value = cipher.Unprotect(stored);

        dbContext.WebhookSecretRevealLogs.Add(WebhookSecretRevealLog.Create(
            subscription.Id,
            command.Field,
            revealedBy,
            dateTimeProvider.ApplicationNow,
            command.IpAddress));
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RevealWebhookSecretResult(value);
    }
}
