namespace Database.Migration;

public interface IEfCoreMigrationService
{
    Task<bool> MigrateAsync(string environment = "Development");
    Task<bool> MigrateAsync(string connectionString, string environment = "Development");
}