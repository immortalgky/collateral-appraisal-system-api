using Shared.CQRS;

namespace Common.Application.Features.Monitoring.GetTopBreaches;

/// <summary>
/// Returns the top N breached tasks across Internal, External, and Followup queues,
/// sorted by OlaVarianceHours descending (most overdue first).
/// Only rows where OlaVarianceHours &gt; 0 (actively breached) are included.
/// </summary>
public record GetTopBreachesQuery(int Limit) : IQuery<IReadOnlyList<TopBreachDto>>;
