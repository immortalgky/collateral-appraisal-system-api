using FluentValidation;

namespace Integration.Application.Features.OutboxMessages.ResendOutboxMessages;

public class ResendOutboxMessagesCommandValidator : AbstractValidator<ResendOutboxMessagesCommand>
{
    public ResendOutboxMessagesCommandValidator()
    {
        // Per-item module validity is NOT checked here — an unknown module is a skipped entry
        // (UnknownModule), not a whole-request 400 (api-contract.md).
        RuleFor(x => x.Items)
            .Must(items => items is { Count: >= 1 and <= 200 })
            .WithMessage("items must contain between 1 and 200 entries.");

        // A null / blank item is a malformed request (400 before anything runs), unlike an unknown-but-present
        // module. Without this, {"items":[null]} threw a NullReferenceException mid-loop (500) after the rows
        // ahead of it had already been committed.
        RuleForEach(x => x.Items)
            .Must(item => item is not null && !string.IsNullOrWhiteSpace(item.Module) && item.Id != Guid.Empty)
            .WithMessage("each item must have a non-empty module and id.");

        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
