namespace Parameter.Data;

public class ParameterDbContext : DbContext
{
    public ParameterDbContext(DbContextOptions<ParameterDbContext> options) : base(options)
    {
    }

    public DbSet<Parameters.Models.Parameter> Parameters => Set<Parameters.Models.Parameter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("parameter");

        modelBuilder.ApplyGlobalConventions();

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(modelBuilder);
    }

}