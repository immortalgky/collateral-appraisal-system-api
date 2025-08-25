using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Collateral.Data.Configurations;

public class VesselAppraisalDetailConfigurations : IEntityTypeConfiguration<VesselAppraisalDetail>
{
    public void Configure(EntityTypeBuilder<VesselAppraisalDetail> builder)
    {
        builder
            .HasOne<RequestAppraisal>()
            .WithOne(p => p.VesselAppraisalDetail)
            .HasForeignKey<VesselAppraisalDetail>(p => p.ApprId)
            .IsRequired();

        builder.Property(p => p.Id).HasColumnName("VesselApprID");

        builder.OwnsOne(p => p.AppraisalDetail, machineAppraisalDetail =>
        {
            machineAppraisalDetail.Property(p => p.CanUse).HasColumnName("CanUse");
            machineAppraisalDetail.Property(p => p.Location).HasColumnName("Location")
                .HasMaxLength(200);
            machineAppraisalDetail.Property(p => p.ConditionUse).HasColumnName("ConditionUse")
                .UseNameConfig();
            machineAppraisalDetail.Property(p => p.UsePurpose).HasColumnName("UsePurpose")
                .UseNameConfig();
            machineAppraisalDetail.Property(p => p.Part).HasColumnName("VesselPart");
            machineAppraisalDetail.Property(p => p.Remark).UseRemarkConfig().HasColumnName("Remark");
            machineAppraisalDetail.Property(p => p.Other).HasColumnName("Other");
            machineAppraisalDetail.Property(p => p.AppraiserOpinion).HasColumnName("AppraiserOpinion");        
        });
    }
}