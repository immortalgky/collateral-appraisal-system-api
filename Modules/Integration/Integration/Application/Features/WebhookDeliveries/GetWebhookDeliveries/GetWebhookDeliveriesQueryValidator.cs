using FluentValidation;
using Integration.Domain.WebhookDeliveries;

namespace Integration.Application.Features.WebhookDeliveries.GetWebhookDeliveries;

public class GetWebhookDeliveriesQueryValidator : AbstractValidator<GetWebhookDeliveriesQuery>
{
    private static readonly string[] AllowedStatuses =
        { DeliveryStatus.Pending, DeliveryStatus.Delivered, DeliveryStatus.Failed };

    public GetWebhookDeliveriesQueryValidator()
    {
        // Handler converts to the 0-based PaginationRequest via PageNumber - 1, so callers send 1-based.
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);

        RuleFor(x => x.Status)
            .Must(s => s is null || AllowedStatuses.Any(a => a.Equals(s, StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Status must be one of: Pending, Delivered, Failed.");

        RuleFor(x => x.EventType).MaximumLength(100);
    }
}
