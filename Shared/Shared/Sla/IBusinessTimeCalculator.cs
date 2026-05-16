namespace Shared.Sla;

public interface IBusinessTimeCalculator
{
    Task<DateTime> AddBusinessHoursAsync(DateTime from, int hours, CancellationToken ct = default);
    Task<int> GetBusinessMinutesBetweenAsync(DateTime from, DateTime to, CancellationToken ct = default);
}
