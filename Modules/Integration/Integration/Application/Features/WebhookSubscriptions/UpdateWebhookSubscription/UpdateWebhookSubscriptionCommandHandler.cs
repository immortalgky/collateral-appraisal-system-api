using FluentValidation;
using Integration.Infrastructure.Repositories;
using MediatR;
using Shared.CQRS;
using Shared.Exceptions;

namespace Integration.Application.Features.WebhookSubscriptions.UpdateWebhookSubscription;

/// <summary>
/// <see cref="SecretKey"/> is optional — supplied only when the admin is replacing the shared secret.
/// When null/blank the existing secret is left untouched.
/// </summary>
public record UpdateWebhookSubscriptionCommand(Guid Id, string CallbackUrl, string? SecretKey) : ICommand;

public class UpdateWebhookSubscriptionCommandValidator : AbstractValidator<UpdateWebhookSubscriptionCommand>
{
    public UpdateWebhookSubscriptionCommandValidator()
    {
        RuleFor(x => x.CallbackUrl)
            .NotEmpty().MaximumLength(500)
            .Must(WebhookUrlRules.BeAnAbsoluteHttpUrl)
            .WithMessage("CallbackUrl must be an absolute http(s) URL.");
        RuleFor(x => x.SecretKey)
            .MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.SecretKey));
    }
}

public class UpdateWebhookSubscriptionCommandHandler(IWebhookSubscriptionRepository repository)
    : ICommandHandler<UpdateWebhookSubscriptionCommand>
{
    public async Task<Unit> Handle(UpdateWebhookSubscriptionCommand command, CancellationToken cancellationToken)
    {
        var subscription = await repository.GetByIdAsync(command.Id, cancellationToken)
                           ?? throw new NotFoundException("WebhookSubscription", command.Id);

        subscription.UpdateCallbackUrl(command.CallbackUrl.Trim());

        if (!string.IsNullOrWhiteSpace(command.SecretKey))
            subscription.UpdateSecretKey(command.SecretKey);

        await repository.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
