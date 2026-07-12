using Dapper;
using FluentValidation;
using Integration.Domain.WebhookSubscriptions;
using Integration.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.CQRS;
using Shared.Data;
using Shared.Exceptions;

namespace Integration.Application.Features.WebhookSubscriptions.CreateWebhookSubscription;

public record CreateWebhookSubscriptionCommand(
    string SystemCode, string CallbackUrl, string SecretKey, string? EventType = null)
    : ICommand<CreateWebhookSubscriptionResult>;

public record CreateWebhookSubscriptionResult(Guid Id);

public class CreateWebhookSubscriptionCommandValidator : AbstractValidator<CreateWebhookSubscriptionCommand>
{
    public CreateWebhookSubscriptionCommandValidator()
    {
        RuleFor(x => x.SystemCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.CallbackUrl)
            .NotEmpty().MaximumLength(500)
            .Must(WebhookUrlRules.BeAnAbsoluteHttpUrl)
            .WithMessage("CallbackUrl must be an absolute http(s) URL.");
        RuleFor(x => x.SecretKey).NotEmpty().MaximumLength(256);
        RuleFor(x => x.EventType).MaximumLength(100);
    }
}

public class CreateWebhookSubscriptionCommandHandler(
    IWebhookSubscriptionRepository repository,
    ISqlConnectionFactory sqlConnectionFactory)
    : ICommandHandler<CreateWebhookSubscriptionCommand, CreateWebhookSubscriptionResult>
{
    public async Task<CreateWebhookSubscriptionResult> Handle(
        CreateWebhookSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        var systemCode = command.SystemCode.Trim();
        var eventType = string.IsNullOrWhiteSpace(command.EventType) ? null : command.EventType.Trim();

        // (SystemCode, EventType) is the routing key the outbound dispatcher looks up — it must be
        // unique across all rows (active or not), so check directly rather than via the
        // active-only repo lookup. Null-safe on EventType (null = catch-all).
        if (await SubscriptionExistsAsync(systemCode, eventType))
            throw new ConflictException("WebhookSubscription", $"{systemCode}/{eventType ?? "(catch-all)"}");

        var subscription = WebhookSubscription.Create(
            systemCode, command.CallbackUrl.Trim(), command.SecretKey, eventType: eventType);

        await repository.AddAsync(subscription, cancellationToken);
        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique index on (SystemCode, EventType) is the atomic backstop: a concurrent
            // insert that won the race surfaces here as a constraint violation — translate to 409.
            if (await SubscriptionExistsAsync(systemCode, eventType))
                throw new ConflictException("WebhookSubscription", $"{systemCode}/{eventType ?? "(catch-all)"}");
            throw;
        }

        return new CreateWebhookSubscriptionResult(subscription.Id);
    }

    private async Task<bool> SubscriptionExistsAsync(string systemCode, string? eventType)
    {
        var connection = sqlConnectionFactory.GetOpenConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(1) FROM integration.WebhookSubscriptions
            WHERE SystemCode = @systemCode
              AND ((EventType = @eventType) OR (EventType IS NULL AND @eventType IS NULL))
            """,
            new { systemCode, eventType });
        return count > 0;
    }
}
