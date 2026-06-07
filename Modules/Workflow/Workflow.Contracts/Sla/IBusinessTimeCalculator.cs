namespace Workflow.Contracts.Sla;

/// <summary>
/// Business-time calculator owned by the Workflow module (sources working hours, lunch and
/// holidays from <c>workflow.BusinessHoursConfigs</c> / <c>workflow.Holidays</c>). Published as a
/// contract so other modules (Appraisal, Reporting) can compute business-time elapsed/remaining
/// without depending on the Workflow implementation.
/// </summary>
public interface IBusinessTimeCalculator
{
    Task<DateTime> AddBusinessHoursAsync(DateTime from, int hours, CancellationToken ct = default);
    Task<int> GetBusinessMinutesBetweenAsync(DateTime from, DateTime to, CancellationToken ct = default);
}
