using Microsoft.EntityFrameworkCore;
using Shared.OutboxPatterns.Configurations;
using Shared.OutboxPatterns.Models;

namespace Shared.OutboxPatterns.Extensions;

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
}