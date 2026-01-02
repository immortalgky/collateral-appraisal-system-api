namespace Database.Migration;

public interface IMigrationService
{
    Task<bool> MigrateAsync(string environment = "Development");
    Task<bool> ValidateAsync();
    Task<bool> RollbackAsync(string targetVersion);
    Task<IEnumerable<MigrationHistory>> GetMigrationHistoryAsync();
    Task<bool> GenerateRollbackScriptAsync(string version, string outputPath);
}