namespace Assignment.Data;

public class AssignmentDbContext : DbContext
{
    public AssignmentDbContext(DbContextOptions<AssignmentDbContext> options) : base(options)
    {
    }

    public DbSet<Assignments.Models.Assignment> Assignments => Set<Assignments.Models.Assignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure the default schema for the database
        modelBuilder.HasDefaultSchema("assignment");

        // Apply global conventions for the model
        modelBuilder.ApplyGlobalConventions();

        // Apply configurations from the current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Call the base method to ensure any additional configurations are applied
        base.OnModelCreating(modelBuilder);
    }
}