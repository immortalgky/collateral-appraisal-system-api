using FluentValidation;

namespace Integration.Application.Features.WebhookDeliveries.GetWebhookDeliverySummary;

public class GetWebhookDeliverySummaryQueryValidator : AbstractValidator<GetWebhookDeliverySummaryQuery>
{
    public GetWebhookDeliverySummaryQueryValidator()
    {
        RuleFor(x => x.EventType).MaximumLength(100);
        RuleFor(x => x.SystemCode).MaximumLength(50);
        RuleFor(x => x.Search).MaximumLength(100);
    }
}
