using Microsoft.Extensions.Logging;
using Reporting.Data;
using Shared.Identity;

namespace Reporting.Application.OperationalReports.Shared;

/// <summary>Persists a report-generation audit row. Never throws — logging must not break an export.</summary>
public interface IReportAuditLogger
{
    Task LogExportAsync(
        string reportName, string format, DateTime generatedAt, int durationMs,
        int rowCount, long? fileSizeBytes, bool success, string? error, CancellationToken ct);
}

internal sealed class ReportAuditLogger(
    ReportingDbContext dbContext,
    ICurrentUserService currentUser,
    ILogger<ReportAuditLogger> logger) : IReportAuditLogger
{
    public async Task LogExportAsync(
        string reportName, string format, DateTime generatedAt, int durationMs,
        int rowCount, long? fileSizeBytes, bool success, string? error, CancellationToken ct)
    {
        try
        {
            dbContext.ReportGenerationLogs.Add(ReportGenerationLog.Create(
                reportName, format, currentUser.UserCode, generatedAt,
                durationMs, rowCount, fileSizeBytes, success, error));
            await dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Audit failure must not surface to the caller or fail the export.
            logger.LogWarning(ex, "Failed to write report-generation audit log for {Report}", reportName);
        }
    }
}
