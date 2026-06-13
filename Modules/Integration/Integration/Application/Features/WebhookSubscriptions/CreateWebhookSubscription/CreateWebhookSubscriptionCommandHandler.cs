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
    string SystemCode, string CallbackUrl, string SecretKey)
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

        // SystemCode is the routing key the outbound dispatcher looks up — it must be unique
        // across all rows (active or not), so check directly rather than via the active-only repo lookup.
        var connection = sqlConnectionFactory.GetOpenConnection();
        var exists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM integration.WebhookSubscriptions WHERE SystemCode = @systemCode",
            new { systemCode });
        if (exists > 0)
            throw new ConflictException("WebhookSubscription", systemCode);

        var subscription = WebhookSubscription.Create(systemCode, command.CallbackUrl.Trim(), command.SecretKey);

        await repository.AddAsync(subscription, cancellationToken);
        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique index on SystemCode is the atomic backstop: a concurrent insert that won
            // the race surfaces here as a constraint violation — translate it to a clean 409.
            if (await SystemCodeExistsAsync(systemCode))
                throw new ConflictException("WebhookSubscription", systemCode);
            throw;
        }

        return new CreateWebhookSubscriptionResult(subscription.Id);
    }

    private async Task<bool> SystemCodeExistsAsync(string systemCode)
    {
        var connection = sqlConnectionFactory.GetOpenConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM integration.WebhookSubscriptions WHERE SystemCode = @systemCode",
            new { systemCode });
        return count > 0;
    }
}
