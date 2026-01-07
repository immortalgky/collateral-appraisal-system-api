namespace Appraisal.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for AppraisalSettings entity.
/// </summary>
public class AppraisalSettingsRepository(AppraisalDbContext dbContext)
    : BaseRepository<AppraisalSettings, Guid>(dbContext), IAppraisalSettingsRepository
{
    private readonly AppraisalDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public async Task<AppraisalSettings?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _dbContext.AppraisalSettings
            .FirstOrDefaultAsync(s => s.SettingKey == key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<AppraisalSettings>> GetAllSettingsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.AppraisalSettings
            .OrderBy(s => s.SettingKey)
            .ToListAsync(cancellationToken);
    }
}