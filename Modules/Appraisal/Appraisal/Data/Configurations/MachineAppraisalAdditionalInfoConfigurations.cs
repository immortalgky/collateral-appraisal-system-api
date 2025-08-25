using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Collateral.Data.Configurations;

public class MachineAppraisalAdditionalInfoConfigurations : IEntityTypeConfiguration<MachineAppraisalAdditionalInfo>
{
    public void Configure(EntityTypeBuilder<MachineAppraisalAdditionalInfo> builder)
    {

        builder.Property(p => p.Id).HasColumnName("MachDetId");

        builder
            .HasOne<RequestAppraisal>()
            .WithOne(p => p.MachineAppraisalAdditionalInfo)
            .HasForeignKey<MachineAppraisalAdditionalInfo>(p => p.ApprId)
            .IsRequired();

        builder.OwnsOne(p => p.PurposeAndLocationMachine, purpose =>
        {
            purpose.Property(p => p.Assignment).HasColumnName("Assignment");
            purpose.Property(p => p.ApprCollatPurpose).HasColumnName("ApprCollatPurpose");
            purpose.Property(p => p.ApprDate).HasColumnName("ApprDate");
            purpose.Property(p => p.ApprCollatType).HasColumnName("ApprCollatType");
        });

        builder.OwnsOne(p => p.MachineDetail, machineDetail =>
        {
            machineDetail.OwnsOne(p => p.GeneralMachinery, general =>
            {
                general.Property(p => p.Industrial).HasColumnName("Industrial")
                    .HasMaxLength(40);
                general.Property(p => p.SurveyNo).HasColumnName("SurveyNo")
                    .UseTinyStringConfig();
                general.Property(p => p.ApprNo).HasColumnName("ApprNo")
                    .UseTinyStringConfig();
            });

            machineDetail.OwnsOne(p => p.AtSurveyDate, surveyDate =>
            {
                surveyDate.Property(p => p.Installed).HasColumnName("Installed")
                    .UseTinyStringConfig();
                surveyDate.Property(p => p.ApprScrap).HasColumnName("ApprScrap")
                    .HasMaxLength(40);
                surveyDate.Property(p => p.NoOfAppraise).HasColumnName("NoOfAppraise")
                    .UseTinyStringConfig();
                surveyDate.Property(p => p.NotInstalled).HasColumnName("NotInstalled")
                    .UseTinyStringConfig();
                surveyDate.Property(p => p.Maintenance).HasColumnName("Maintenance")
                    .HasMaxLength(40);
                surveyDate.Property(p => p.Exterior).HasColumnName("Exterior")
                    .HasMaxLength(40);
                surveyDate.Property(p => p.Performance).HasColumnName("Performance")
                    .HasMaxLength(40);
                surveyDate.Property(p => p.MarketDemand).HasColumnName("MarketDemand");
                surveyDate.Property(p => p.MarketDemandRemark).HasColumnName("MarketDemandRemark");
            });

            machineDetail.OwnsOne(p => p.RightsAndConditionsOfLegalRestrictions, rightsAndConditions =>
            {
                rightsAndConditions.Property(p => p.Proprietor).HasColumnName("Proprietor")
                    .HasMaxLength(40);
                rightsAndConditions.Property(p => p.Owner).HasColumnName("Owner")
                    .HasMaxLength(40);
                rightsAndConditions.Property(p => p.MachineLocation).HasColumnName("MachineLocation")
                    .HasMaxLength(100);
                rightsAndConditions.Property(p => p.Obligation).HasColumnName("Obligation")
                    .HasMaxLength(100);
                rightsAndConditions.Property(p => p.Other).HasColumnName("Other")
                    .HasMaxLength(100);
            });

            machineDetail.Navigation(p => p.GeneralMachinery).IsRequired();
            machineDetail.Navigation(p => p.AtSurveyDate).IsRequired();
            machineDetail.Navigation(p => p.RightsAndConditionsOfLegalRestrictions).IsRequired();
        });

    }
}