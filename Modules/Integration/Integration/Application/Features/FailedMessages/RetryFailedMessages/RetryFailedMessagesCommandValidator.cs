using FluentValidation;

namespace Integration.Application.Features.FailedMessages.RetryFailedMessages;

public class RetryFailedMessagesCommandValidator : AbstractValidator<RetryFailedMessagesCommand>
{
    public RetryFailedMessagesCommandValidator()
    {
        RuleFor(x => x.Ids)
            .Must(ids => ids is { Count: >= 1 and <= 200 })
            .WithMessage("ids must contain between 1 and 200 entries.");

        // Matches FailedMessage.ActionReason's column length.
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
