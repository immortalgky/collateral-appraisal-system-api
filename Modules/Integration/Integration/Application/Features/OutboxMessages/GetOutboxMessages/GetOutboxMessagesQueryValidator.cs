using FluentValidation;

namespace Integration.Application.Features.OutboxMessages.GetOutboxMessages;

public class GetOutboxMessagesQueryValidator : AbstractValidator<GetOutboxMessagesQuery>
{
    private static readonly string[] AllowedStatuses = ["Failed", "Stuck", "Resent", "All"];

    public GetOutboxMessagesQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);

        RuleFor(x => x.Status)
            .Must(s => AllowedStatuses.Contains(s))
            .WithMessage("Status must be one of: Failed, Stuck, Resent, All.");

        RuleFor(x => x.Module)
            // Blank means "all modules" (the handler treats it the same as null).
            .Must(m => string.IsNullOrWhiteSpace(m) || OutboxModuleWhitelist.IsValid(m))
            .WithMessage(
                $"Module must be one of: {string.Join(", ", OutboxModuleWhitelist.Modules)}.");
    }
}
