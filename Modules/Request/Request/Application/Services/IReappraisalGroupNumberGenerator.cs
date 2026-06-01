namespace Request.Application.Services;

public interface IReappraisalGroupNumberGenerator
{
    /// <summary>
    /// Generates the next reappraisal group number within the caller's transaction.
    /// Uses UPDLOCK/ROWLOCK/HOLDLOCK on dbo.RunningNumbers to prevent gaps or duplicates.
    /// </summary>
    Task<string> GenerateAsync(CancellationToken cancellationToken = default);
}
