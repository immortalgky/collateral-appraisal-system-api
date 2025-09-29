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

        builder.OwnsOne(p => p.AppraisalDetail, vehicleAppraisalDetail =>
        {
            vehicleAppraisalDetail.Property(p => p.CanUse).HasColumnName("CanUse");
            vehicleAppraisalDetail.Property(p => p.Location).HasColumnName("Location")
                .HasMaxLength(200);
            vehicleAppraisalDetail.Property(p => p.ConditionUse).HasColumnName("ConditionUse")
                .UseNameConfig();
            vehicleAppraisalDetail.Property(p => p.UsePurpose).HasColumnName("UsePurpose")
                .UseNameConfig();
            vehicleAppraisalDetail.Property(p => p.Part).HasColumnName("VehiclePart");
            vehicleAppraisalDetail.Property(p => p.Remark).UseRemarkConfig().HasColumnName("Remark");
            vehicleAppraisalDetail.Property(p => p.Other).HasColumnName("Other");
            vehicleAppraisalDetail.Property(p => p.AppraiserOpinion).HasColumnName("AppraiserOpinion");        
        });
    }
}