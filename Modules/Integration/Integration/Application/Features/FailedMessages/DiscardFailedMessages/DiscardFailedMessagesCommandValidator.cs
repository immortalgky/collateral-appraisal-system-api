using FluentValidation;

namespace Integration.Application.Features.FailedMessages.DiscardFailedMessages;

public class DiscardFailedMessagesCommandValidator : AbstractValidator<DiscardFailedMessagesCommand>
{
    public DiscardFailedMessagesCommandValidator()
    {
        RuleFor(x => x.Ids)
            .Must(ids => ids is { Count: >= 1 and <= 200 })
            .WithMessage("ids must contain between 1 and 200 entries.");

        // Matches FailedMessage.ActionReason's column length.
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
