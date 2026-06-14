using Document.Domain.UploadSessions.Model;
using Document.Domain.Documents.Models;
using Shared.Data.Outbox;
using Shared.Scheduling;

namespace Document.Data;

public class DocumentDbContext : DbContext
{
    public DocumentDbContext(DbContextOptions<DocumentDbContext> options) : base(options)
    {
    }

    public DbSet<Domain.Documents.Models.Document> Documents => Set<Domain.Documents.Models.Document>();
    public DbSet<UploadSession> UploadSessions => Set<UploadSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("document");

        modelBuilder.ApplyGlobalConventions();

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Integration event outbox for reliable messaging
        modelBuilder.AddIntegrationEventOutbox();

        // Per-module recurring-job schedule table (document.JobSchedules)
        modelBuilder.AddJobSchedules();

        base.OnModelCreating(modelBuilder);
    }
}