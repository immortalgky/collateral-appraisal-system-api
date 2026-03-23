using Parameter.ConstructionWork.Models;

namespace Parameter.Data.Configurations;

public class ConstructionWorkGroupConfiguration : IEntityTypeConfiguration<ConstructionWorkGroup>
{
    public void Configure(EntityTypeBuilder<ConstructionWorkGroup> builder)
    {
        builder.ToTable("ConstructionWorkGroups");

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(g => g.Code).IsRequired().HasMaxLength(50);
        builder.Property(g => g.NameTh).IsRequired().HasMaxLength(200);
        builder.Property(g => g.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(g => g.DisplayOrder).IsRequired();
        builder.Property(g => g.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasIndex(g => g.Code).IsUnique();

        builder.HasMany(g => g.WorkItems)
            .WithOne()
            .HasForeignKey(i => i.ConstructionWorkGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ConstructionWorkItemConfiguration : IEntityTypeConfiguration<ConstructionWorkItem>
{
    public void Configure(EntityTypeBuilder<ConstructionWorkItem> builder)
    {
        builder.ToTable("ConstructionWorkItems");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(i => i.ConstructionWorkGroupId).IsRequired();
        builder.Property(i => i.Code).IsRequired().HasMaxLength(50);
        builder.Property(i => i.NameTh).IsRequired().HasMaxLength(200);
        builder.Property(i => i.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(i => i.DisplayOrder).IsRequired();
        builder.Property(i => i.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasIndex(i => new { i.ConstructionWorkGroupId, i.Code }).IsUnique();
    }
}
