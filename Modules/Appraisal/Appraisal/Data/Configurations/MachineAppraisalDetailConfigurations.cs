using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Appraisal.Data.Configurations;

public class MachineAppraisalDetailConfigurations : IEntityTypeConfiguration<MachineAppraisalDetail>
{
    public void Configure(EntityTypeBuilder<MachineAppraisalDetail> builder)
    {
        builder.Property(p => p.Id).HasColumnName("MachineApprID")
            .UseIdentityColumn();

        builder
            .HasOne<RequestAppraisal>()
            .WithOne(p => p.MachineAppraisalDetail)
            .HasForeignKey<MachineAppraisalDetail>(p => p.ApprId)
            .IsRequired();
        
        builder.OwnsOne(p => p.AppraisalDetail, machineApprDetail =>
        {
            machineApprDetail.Property(p => p.CanUse).HasColumnName("CanUse");
            machineApprDetail.Property(p => p.Location).HasColumnName("Location")
                .UseNameConfig();
            machineApprDetail.Property(p => p.ConditionUse).HasColumnName("ConditionUse")
                .UseNameConfig();
            machineApprDetail.Property(p => p.UsePurpose).HasColumnName("UsePurpose")
                .UseNameConfig();
            machineApprDetail.Property(p => p.Part).HasColumnName("MachinePart");
            machineApprDetail.Property(p => p.Remark).UseRemarkConfig().HasColumnName("Remark");
            machineApprDetail.Property(p => p.Other).HasColumnName("Other");
            machineApprDetail.Property(p => p.AppraiserOpinion).HasColumnName("AppraiserOpinion");        
        });
    }
}