using Shared.Data.Seed;

namespace Parameter.Data.Seed;

public class ParameterDataSeed(ParameterDbContext context) : IDataSeeder<ParameterDbContext>
{
    public async Task SeedAllAsync()
    {
        if (!await context.Parameters.AnyAsync())
        {
            await context.Parameters.AddRangeAsync(InitialData.Parameters);
            await context.SaveChangesAsync();   
        }
    }
}