using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Appraisal.Data.Configurations;

public class VehicleAppraisalDetailConfigurations : IEntityTypeConfiguration<VehicleAppraisalDetail>
{
    public void Configure(EntityTypeBuilder<VehicleAppraisalDetail> builder)
    {
        builder
            .HasOne<RequestAppraisal>()
            .WithOne(p => p.VehicleAppraisalDetail)
            .HasForeignKey<VehicleAppraisalDetail>(p => p.ApprId)
            .IsRequired();

        builder.Property(p => p.Id).HasColumnName("VehicleApprID");

        builder.OwnsOne(p => p.AppraisalDetail, machineAppraisalDetail =>
        {
            machineAppraisalDetail.Property(p => p.CanUse).HasColumnName("CanUse");
            machineAppraisalDetail.Property(p => p.Location).HasColumnName("Location")
                .HasMaxLength(200);
            machineAppraisalDetail.Property(p => p.ConditionUse).HasColumnName("ConditionUse")
                .UseNameConfig();
            machineAppraisalDetail.Property(p => p.UsePurpose).HasColumnName("UsePurpose")
                .UseNameConfig();
            machineAppraisalDetail.Property(p => p.Part).HasColumnName("VehiclePart");
            machineAppraisalDetail.Property(p => p.Remark).UseRemarkConfig().HasColumnName("Remark");
            machineAppraisalDetail.Property(p => p.Other).HasColumnName("Other");
            machineAppraisalDetail.Property(p => p.AppraiserOpinion).HasColumnName("AppraiserOpinion");        
        });
    }
}