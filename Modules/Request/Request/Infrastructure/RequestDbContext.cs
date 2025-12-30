using MassTransit;

namespace Request.Infrastructure;

public class RequestDbContext(DbContextOptions<RequestDbContext> options) : DbContext(options)
{
    public DbSet<Domain.Requests.Request> Requests => Set<Domain.Requests.Request>();
    public DbSet<RequestTitle> RequestTitles => Set<RequestTitle>();
    public DbSet<RequestDocument> RequestDocuments => Set<RequestDocument>();
    public DbSet<RequestComment> RequestComments => Set<RequestComment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure the default schema for the database
        modelBuilder.HasDefaultSchema("request");

        // Apply global conventions for the model
        modelBuilder.ApplyGlobalConventions();

        // Apply configurations from the current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Global query filter for soft delete
        modelBuilder.Entity<Domain.Requests.Request>()
            .HasQueryFilter(r => !r.SoftDelete.IsDeleted);

        // Add MassTransit Outbox entities
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();

        // Call the base method to ensure any additional configurations are applied
        base.OnModelCreating(modelBuilder);
    }
}