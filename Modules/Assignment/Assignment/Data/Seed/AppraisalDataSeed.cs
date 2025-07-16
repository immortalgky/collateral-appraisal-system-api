namespace Assignment.Data.Seed;

public class RequestDataSeed(AssignmentDbContext context) : IDataSeeder<AssignmentDbContext>
{
    public async Task SeedAllAsync()
    {
        if (!await context.Assignments.AnyAsync())
        {
            // await context.Assignments.AddRangeAsync(InitialData.Assignments);
            await context.SaveChangesAsync();
        }
    }
}