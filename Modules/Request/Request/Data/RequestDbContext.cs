namespace Request.Data;

public class RequestDbContext : DbContext
{
    public RequestDbContext(DbContextOptions<RequestDbContext> options) : base(options)
    {
    }

    public DbSet<Requests.Models.Request> Requests => Set<Requests.Models.Request>();
    public DbSet<RequestComment> RequestComments => Set<RequestComment>();
    public DbSet<RequestTitle> RequestTitles => Set<RequestTitle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure the default schema for the database
        modelBuilder.HasDefaultSchema("request");

        // Apply global conventions for the model
        modelBuilder.ApplyGlobalConventions();

        // Apply configurations from the current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Call the base method to ensure any additional configurations are applied
        base.OnModelCreating(modelBuilder);
    }
}