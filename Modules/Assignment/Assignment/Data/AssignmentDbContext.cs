namespace Assignment.Data;

<<<<<<< HEAD
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
=======
public class AssignmentDbContext(DbContextOptions<AssignmentDbContext> options) : DbContext(options)
{
    public DbSet<PendingTask> PendingTasks => Set<PendingTask>();
    public DbSet<CompletedTask> CompletedTasks => Set<CompletedTask>();
    public DbSet<RoundRobinQueue> RoundRobinQueue => Set<RoundRobinQueue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("assignment");

        modelBuilder.ApplyGlobalConventions();

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

>>>>>>> 3434ca92f42e614d268511d3a6e0d95cb6f4d666
        base.OnModelCreating(modelBuilder);
    }
}