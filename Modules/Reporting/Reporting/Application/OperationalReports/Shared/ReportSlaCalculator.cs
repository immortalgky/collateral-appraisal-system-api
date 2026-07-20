using Shared.Time;
using Workflow.Contracts.Sla;

namespace Reporting.Application.OperationalReports.Shared;

/// <summary>Per-appraisal inputs for the SLA elapsed calculation (RCAS007/012).</summary>
public sealed record SlaInput(Guid AppraisalId, DateTime? AppointmentDate, DateTime? SubmittedAt);

/// <summary>
/// Computes the RCAS007/012 SLA value: the BUSINESS-time elapsed from the appointment date to
/// first submission (or to now when not yet submitted), expressed in days at 8 business hours/day.
/// Business hours (weekends/holidays/lunch excluded) come from <see cref="IBusinessTimeCalculator"/>,
/// the same engine the OLA segments use — a plain DATEDIFF would count calendar time.
/// (FSD calls this "SLA" on RCAS007 and "OLA" on RCAS012; per business they are the same measure.)
/// </summary>
public interface IReportSlaCalculator
{
    Task<IReadOnlyDictionary<Guid, decimal?>> ComputeAsync(
        IReadOnlyList<SlaInput> inputs, CancellationToken ct);
}

internal sealed class ReportSlaCalculator(
    IBusinessTimeCalculator businessTime, IDateTimeProvider dateTimeProvider) : IReportSlaCalculator
{
    public async Task<IReadOnlyDictionary<Guid, decimal?>> ComputeAsync(
        IReadOnlyList<SlaInput> inputs, CancellationToken ct)
    {
        var now = dateTimeProvider.ApplicationNow;
        var result = new Dictionary<Guid, decimal?>(inputs.Count);

        foreach (var i in inputs)
        {
            var to = i.SubmittedAt ?? now;
            if (i.AppointmentDate is null || to <= i.AppointmentDate)
            {
                result[i.AppraisalId] = null;
                continue;
            }

            var minutes = await businessTime.GetBusinessMinutesBetweenAsync(i.AppointmentDate.Value, to, ct);
            result[i.AppraisalId] = Math.Round((decimal)minutes / 60m / 8m, 2);
        }

        return result;
    }
}
