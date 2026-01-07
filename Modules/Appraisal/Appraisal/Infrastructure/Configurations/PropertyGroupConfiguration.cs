namespace Appraisal.Infrastructure.Configurations;

/// <summary>
/// EF Core configuration for the PropertyGroup entity.
/// </summary>
public class PropertyGroupConfiguration : IEntityTypeConfiguration<PropertyGroup>
{
    public void Configure(EntityTypeBuilder<PropertyGroup> builder)
    {
        builder.ToTable("PropertyGroups");

        // Primary Key
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Core Properties
        builder.Property(g => g.AppraisalId)
            .IsRequired();

        builder.Property(g => g.GroupNumber)
            .IsRequired();

        builder.Property(g => g.GroupName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(g => g.Description)
            .HasMaxLength(500);

        builder.Property(g => g.UseSystemCalc)
            .IsRequired()
            .HasDefaultValue(true);

        // Audit Fields
        builder.Property(g => g.CreatedOn)
            .IsRequired();
        builder.Property(g => g.CreatedBy)
            .IsRequired();
        builder.Property(g => g.UpdatedOn);
        builder.Property(g => g.UpdatedBy);

        // Navigation to items
        builder.HasMany(g => g.Items)
            .WithOne()
            .HasForeignKey(i => i.PropertyGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(g => g.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Indexes
        builder.HasIndex(g => g.AppraisalId);
        builder.HasIndex(g => new { g.AppraisalId, g.GroupNumber })
            .IsUnique();
    }
}

/// <summary>
/// EF Core configuration for the PropertyGroupItem junction entity.
/// </summary>
public class PropertyGroupItemConfiguration : IEntityTypeConfiguration<PropertyGroupItem>
{
    public void Configure(EntityTypeBuilder<PropertyGroupItem> builder)
    {
        builder.ToTable("PropertyGroupItems");

        // Primary Key
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Core Properties
        builder.Property(i => i.PropertyGroupId)
            .IsRequired();

        builder.Property(i => i.AppraisalPropertyId)
            .IsRequired();

        builder.Property(i => i.SequenceInGroup)
            .IsRequired();

        // Audit Fields
        builder.Property(i => i.CreatedOn)
            .IsRequired();
        builder.Property(i => i.CreatedBy)
            .IsRequired();

        // Indexes
        builder.HasIndex(i => i.PropertyGroupId);
        builder.HasIndex(i => i.AppraisalPropertyId);
        builder.HasIndex(i => new { i.PropertyGroupId, i.AppraisalPropertyId })
            .IsUnique();
    }
}