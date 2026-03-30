namespace Workflow.Sla.Services;

public interface IBusinessTimeCalculator
{
    Task<DateTime> AddBusinessHoursAsync(DateTime from, int hours, CancellationToken ct = default);
}
