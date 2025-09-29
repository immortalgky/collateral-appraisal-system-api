namespace Shared.Messaging.OutboxPatterns.Extensions;

public static class ModelBuilderExtensions
{
    /// <summary>
    /// Configure OutboxMessage entity for the specified schema
    /// </summary>
    /// <param name="modelBuilder">The ModelBuilder instance</param>
    /// <param name="schema">Schema name for the OutboxMessages table</param>
    /// <returns>ModelBuilder for method chaining</returns>
    public static ModelBuilder ConfigureOutboxMessage(this ModelBuilder modelBuilder, string schema)
    {
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration(schema));
        return modelBuilder;
    }

    /// <summary>
    /// Add OutboxMessage DbSet and configuration to DbContext
    /// </summary>
    /// <param name="modelBuilder">The ModelBuilder instance</param>
    /// <param name="schema">Schema name for the OutboxMessages table</param>
    /// <returns>ModelBuilder for method chaining</returns>
    public static ModelBuilder AddOutboxSupport(this ModelBuilder modelBuilder, string schema)
    {
        // Ensure OutboxMessage is included in the model
        modelBuilder.Entity<OutboxMessage>();
        
        // Apply configuration
        modelBuilder.ConfigureOutboxMessage(schema);
        
        return modelBuilder;
    }

    /// <summary>
    /// Configure InboxMessage entity for the specified schema
    /// </summary>
    /// <param name="modelBuilder">The ModelBuilder instance</param>
    /// <param name="schema">Schema name for the InboxMessages table</param>
    /// <returns>ModelBuilder for method chaining</returns>
    public static ModelBuilder ConfigureInboxMessage(this ModelBuilder modelBuilder, string schema)
    {
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration(schema));
        return modelBuilder;
    }

    /// <summary>
    /// Add InboxMessage DbSet and configuration to DbContext
    /// </summary>
    /// <param name="modelBuilder">The ModelBuilder instance</param>
    /// <param name="schema">Schema name for the InboxMessages table</param>
    /// <returns>ModelBuilder for method chaining</returns>
    public static ModelBuilder AddInboxSupport(this ModelBuilder modelBuilder, string schema)
    {
        // Ensure InboxMessage is included in the model
        modelBuilder.Entity<InboxMessage>();
        
        // Apply configuration
        modelBuilder.ConfigureInboxMessage(schema);
        
        return modelBuilder;
    }
}