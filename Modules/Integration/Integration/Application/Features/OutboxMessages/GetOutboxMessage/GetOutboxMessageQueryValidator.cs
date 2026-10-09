using FluentValidation;

namespace Integration.Application.Features.OutboxMessages.GetOutboxMessage;

public class GetOutboxMessageQueryValidator : AbstractValidator<GetOutboxMessageQuery>
{
    public GetOutboxMessageQueryValidator()
    {
        RuleFor(x => x.Module)
            .Must(OutboxModuleWhitelist.IsValid)
            .WithMessage(
                $"Module must be one of: {string.Join(", ", OutboxModuleWhitelist.Modules)}.");
    }
}
