using FluentValidation;
using Integration.Domain.FailedMessages;

namespace Integration.Application.Features.FailedMessages.GetFailedMessages;

public class GetFailedMessagesQueryValidator : AbstractValidator<GetFailedMessagesQuery>
{
    private static readonly string[] AllowedStatuses =
    [
        FailedMessageStatus.Pending, FailedMessageStatus.RetryRequested, FailedMessageStatus.Retried,
        FailedMessageStatus.Discarded, "All"
    ];

    public GetFailedMessagesQueryValidator()
    {
        // Handler converts to the 0-based PaginationRequest via PageNumber - 1, so callers send 1-based.
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);

        RuleFor(x => x.Status)
            .Must(s => AllowedStatuses.Contains(s))
            .WithMessage("Status must be one of: Pending, RetryRequested, Retried, Discarded, All.");
    }
}
