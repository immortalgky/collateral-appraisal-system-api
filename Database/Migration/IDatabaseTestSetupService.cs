namespace Database.Migration;

public interface IDatabaseTestSetupService
{
    Task<bool> SetupDatabaseAsync(string connectionString);
}