using Collateral.CollateralMasters.Models;

namespace Collateral.CollateralMasters.Configurations;

public class ProjectDetailConfiguration : IEntityTypeConfiguration<ProjectDetail>
{
    public void Configure(EntityTypeBuilder<ProjectDetail> builder)
    {
        builder.ToTable("ProjectDetails");

        builder.HasKey(d => d.CollateralMasterId);

        builder.Property(d => d.ProjectType).IsRequired().HasMaxLength(50);
        builder.Property(d => d.ProjectName).HasMaxLength(300);
        builder.Property(d => d.Developer).HasMaxLength(300);
        builder.Property(d => d.Address).HasMaxLength(500);
        builder.Property(d => d.Province).HasMaxLength(100);
        builder.Property(d => d.Latitude).HasPrecision(9, 6);
        builder.Property(d => d.Longitude).HasPrecision(9, 6);
        builder.Property(d => d.TotalUnits).IsRequired();
        builder.Property(d => d.RemainingUnits).IsRequired();
        builder.Property(d => d.ProjectSellingPrice).HasPrecision(18, 2);
        builder.Property(d => d.StructureJson).IsRequired().HasColumnType("nvarchar(max)");

        // AppraisalSummary (owned — flat columns, same pattern as CondoDetail)
        builder.OwnsOne(d => d.AppraisalSummary, s =>
        {
            s.Property(x => x.LastAppraisalId).HasColumnName("LastAppraisalId");
            s.Property(x => x.LastAppraisalNumber).HasColumnName("LastAppraisalNumber").HasMaxLength(50);
            s.Property(x => x.LastAppraisedDate).HasColumnName("LastAppraisedDate");
        });

        builder.Property(d => d.IsDeleted).IsRequired().HasDefaultValue(false);
    }
}
