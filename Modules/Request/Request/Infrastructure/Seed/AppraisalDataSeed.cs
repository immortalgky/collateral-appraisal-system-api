namespace Request.Infrastructure.Seed;

public class RequestDataSeed(RequestDbContext context) : IDataSeeder<RequestDbContext>
{
    public async Task SeedAllAsync()
    {
        if (!await context.Requests.AnyAsync())
        {
            await context.Requests.AddRangeAsync(InitialData.Requests);
            await context.SaveChangesAsync();
        }
    }
}