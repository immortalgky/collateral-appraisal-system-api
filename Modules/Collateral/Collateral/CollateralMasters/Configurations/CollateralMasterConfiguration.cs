using Collateral.CollateralMasters.Models;

namespace Collateral.CollateralMasters.Configurations;

public class CollateralMasterConfiguration : IEntityTypeConfiguration<CollateralMaster>
{
    public void Configure(EntityTypeBuilder<CollateralMaster> builder)
    {
        builder.ToTable("CollateralMasters");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.CollateralType).IsRequired().HasMaxLength(20);
        builder.Property(m => m.OwnerName).HasMaxLength(200);
        builder.Property(m => m.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.Property(m => m.IsMaster)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(m => m.ParentMasterId)
            .HasColumnType("uniqueidentifier")
            .IsRequired(false);

        builder.Property(m => m.CreatedAt).HasColumnName("CreatedOn");
        builder.Property(m => m.UpdatedAt).HasColumnName("UpdatedOn");
        builder.Property(m => m.CreatedBy).HasMaxLength(100);
        builder.Property(m => m.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(m => m.CollateralType);
        builder.HasIndex(m => m.IsDeleted);
        builder.HasIndex(m => m.IsMaster);
        builder.HasIndex(m => m.ParentMasterId)
            .HasDatabaseName("IX_CollateralMasters_ParentMasterId");

        // Self-FK: aliases point at their master. RESTRICT delete so you cannot delete
        // a master while aliases still point to it.
        builder.HasOne<CollateralMaster>()
            .WithMany()
            .HasForeignKey(m => m.ParentMasterId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false)
            .HasConstraintName("FK_CollateralMasters_ParentMaster");

        // Navigation: Engagements
        builder.HasMany(m => m.Engagements)
            .WithOne()
            .HasForeignKey(e => e.CollateralMasterId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(m => m.Engagements).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Navigation: AuditLogs
        builder.HasMany(m => m.AuditLogs)
            .WithOne()
            .HasForeignKey(a => a.CollateralMasterId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(m => m.AuditLogs).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Navigation: LandDetail (1:1)
        builder.HasOne(m => m.LandDetail)
            .WithOne()
            .HasForeignKey<LandDetail>(d => d.CollateralMasterId)
            .OnDelete(DeleteBehavior.Cascade);

        // Navigation: CondoDetail (1:1)
        builder.HasOne(m => m.CondoDetail)
            .WithOne()
            .HasForeignKey<CondoDetail>(d => d.CollateralMasterId)
            .OnDelete(DeleteBehavior.Cascade);

        // Navigation: LeaseholdDetail (1:1)
        builder.HasOne(m => m.LeaseholdDetail)
            .WithOne()
            .HasForeignKey<LeaseholdDetail>(d => d.CollateralMasterId)
            .OnDelete(DeleteBehavior.Cascade);

        // Navigation: MachineDetail (1:1)
        builder.HasOne(m => m.MachineDetail)
            .WithOne()
            .HasForeignKey<MachineDetail>(d => d.CollateralMasterId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ignore EF backing field for domain events
        builder.Ignore(m => m.DomainEvents);
    }
}
