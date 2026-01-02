namespace Appraisal.Data.Configurations;

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

        builder.OwnsOne(p => p.AppraisalDetail, vesselAppraisalDetail =>
        {
            vesselAppraisalDetail.Property(p => p.CanUse).HasColumnName("CanUse");
            vesselAppraisalDetail.Property(p => p.Location).HasColumnName("Location")
                .HasMaxLength(200);
            vesselAppraisalDetail.Property(p => p.ConditionUse).HasColumnName("ConditionUse")
                .UseNameConfig();
            vesselAppraisalDetail.Property(p => p.UsePurpose).HasColumnName("UsePurpose")
                .UseNameConfig();
            vesselAppraisalDetail.Property(p => p.Part).HasColumnName("VesselPart");
            vesselAppraisalDetail.Property(p => p.Remark).UseRemarkConfig().HasColumnName("Remark");
            vesselAppraisalDetail.Property(p => p.Other).HasColumnName("Other");
            vesselAppraisalDetail.Property(p => p.AppraiserOpinion).HasColumnName("AppraiserOpinion");        
        });
    }
}