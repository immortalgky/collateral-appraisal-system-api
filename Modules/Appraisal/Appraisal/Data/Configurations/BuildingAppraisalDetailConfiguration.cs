using Appraisal.AppraisalProperties.Models;

namespace Appraisal.Data.Configurations;

public class BuildingAppraisalDetailConfiguration
    : IEntityTypeConfiguration<BuildingAppraisalDetail>
{
    public void Configure(EntityTypeBuilder<BuildingAppraisalDetail> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).UseIdentityColumn().HasColumnName("BuildingApprId");

        builder
            .HasOne<RequestAppraisal>()
            .WithOne(p => p.BuildingAppraisalDetail)
            .HasForeignKey<BuildingAppraisalDetail>(p => p.ApprId);

        builder.OwnsOne(
            p => p.BuildingInformation,
            information =>
            {
                information
                    .Property(p => p.NoHouseNumber)
                    .UseCodeConfig()
                    .HasColumnName("NoHouseNumber");

                information.Property(p => p.LandArea).UseAreaConfig().HasColumnName("LandArea");

                information
                    .Property(p => p.BuildingCondition)
                    .UseCodeConfig()
                    .HasColumnName("BuildingCondition");

                information
                    .Property(p => p.BuildingStatus)
                    .UseCodeConfig()
                    .HasColumnName("BuildingStatus");

                information
                    .Property(p => p.LicenseExpirationDate)
                    .HasColumnName("LicenseExpirationDate");

                information.Property(p => p.IsAppraise).UseCodeConfig().HasColumnName("IsAppraise");

                information.OwnsOne(
                    p => p.ObligationDetail,
                    detail =>
                    {
                        detail
                            .Property(p => p.IsObligation)
                            .UseCodeConfig()
                            .HasColumnName("IsObligation");

                        detail
                            .Property(p => p.Obligation)
                            .UseObligationConfig()
                            .HasColumnName("Obligation");
                    }
                );
            }
        );

        builder.OwnsOne(
            p => p.BuildingTypeDetail,
            detail =>
            {
                detail.Property(p => p.BuildingType).UseCodeConfig().HasColumnName("BuildingType");

                detail
                    .Property(p => p.BuildingTypeOther)
                    .UseMaxLengthNVarcharConfig()
                    .HasColumnName("BuildingTypeOther");
            }
        );

        builder.OwnsOne(
            p => p.DecorationDetail,
            detail =>
            {
                detail.Property(p => p.Decoration).UseCodeConfig().HasColumnName("Decoration");

                detail
                    .Property(p => p.DecorationOther)
                    .UseMaxLengthNVarcharConfig()
                    .HasColumnName("DecorationOther");
            }
        );

        builder.OwnsOne(
            p => p.Encroachment,
            encroachment =>
            {
                encroachment.Property(p => p.IsEncroached).HasColumnName("IsEncroached");

                encroachment
                    .Property(p => p.IsEncroachedRemark)
                    .UseRemarkConfig()
                    .HasColumnName("IsEncroachedRemark");

                encroachment
                    .Property(p => p.EncroachArea)
                    .UseAreaConfig()
                    .HasColumnName("EncroachArea");
            }
        );

        builder.OwnsOne(
            p => p.BuildingConstructionInformation,
            information =>
            {
                information
                    .Property(p => p.OriginalBuildingPct)
                    .UseMeasurementConfig()
                    .HasColumnName("OriginalBuildingPct");

                information
                    .Property(p => p.UnderConstPct)
                    .UseMeasurementConfig()
                    .HasColumnName("UnderConstPct");
            }
        );

        builder.Property(p => p.BuildingMaterial).UseCodeConfig();

        builder.Property(p => p.BuildingStyle).UseCodeConfig();

        builder.OwnsOne(
            p => p.ResidentialStatus,
            status =>
            {
                status
                    .Property(p => p.IsResidential)
                    .UseCodeConfig()
                    .HasColumnName("IsResidential");

                status.Property(p => p.BuildingYear).HasColumnName("BuildingYear");

                status.Property(p => p.DueTo).UseMaxLengthNVarcharConfig().HasColumnName("DueTo");
            }
        );

        builder.OwnsOne(
            p => p.BuildingStructureDetail,
            detail =>
            {
                detail.OwnsOne(
                    p => p.BuildingConstructionStyle,
                    style =>
                    {
                        style
                            .Property(p => p.ConstStyle)
                            .UseCodeConfig()
                            .HasColumnName("ConstStyle");

                        style
                            .Property(p => p.ConstStyleRemark)
                            .UseRemarkConfig()
                            .HasColumnName("ConstStyleRemark");
                    }
                );

                detail.OwnsOne(
                    p => p.BuildingGeneralStructure,
                    structure =>
                    {
                        structure
                            .Property(p => p.GeneralStructure)
                            .UseMultiSelectConfig()
                            .HasColumnName("GeneralStructure");

                        structure
                            .Property(p => p.GeneralStructureOther)
                            .UseMaxLengthNVarcharConfig()
                            .HasColumnName("GeneralStructureOther");
                    }
                );

                detail.OwnsOne(
                    p => p.BuildingRoofFrame,
                    frame =>
                    {
                        frame
                            .Property(p => p.RoofFrame)
                            .UseMultiSelectConfig()
                            .HasColumnName("RoofFrame");

                        frame
                            .Property(p => p.RoofFrameOther)
                            .UseMaxLengthNVarcharConfig()
                            .HasColumnName("RoofFrameOther");
                    }
                );

                detail.OwnsOne(
                    p => p.BuildingRoof,
                    roof =>
                    {
                        roof.Property(p => p.Roof).UseMultiSelectConfig().HasColumnName("Roof");

                        roof.Property(p => p.RoofOther)
                            .UseMaxLengthNVarcharConfig()
                            .HasColumnName("RoofOther");
                    }
                );

                detail.OwnsOne(
                    p => p.BuildingCeiling,
                    ceiling =>
                    {
                        ceiling
                            .Property(p => p.Ceiling)
                            .UseMultiSelectConfig()
                            .HasColumnName("Ceiling");

                        ceiling
                            .Property(p => p.CeilingOther)
                            .UseMaxLengthNVarcharConfig()
                            .HasColumnName("CeilingOther");
                    }
                );

                detail.OwnsOne(
                    p => p.BuildingWall,
                    wall =>
                    {
                        wall.Property(p => p.InteriorWall)
                            .UseMultiSelectConfig()
                            .HasColumnName("InteriorWall");

                        wall.Property(p => p.InteriorWallOther)
                            .UseMaxLengthNVarcharConfig()
                            .HasColumnName("InteriorWallOther");

                        wall.Property(p => p.ExteriorWall)
                            .UseMultiSelectConfig()
                            .HasColumnName("ExteriorWall");

                        wall.Property(p => p.ExteriorWallOther)
                            .UseMaxLengthNVarcharConfig()
                            .HasColumnName("ExteriorWallOther");
                    }
                );

                detail.OwnsOne(
                    p => p.BuildingFence,
                    fence =>
                    {
                        fence.Property(p => p.Fence).UseMultiSelectConfig().HasColumnName("Fence");

                        fence
                            .Property(p => p.FenceOther)
                            .UseMaxLengthNVarcharConfig()
                            .HasColumnName("FenceOther");
                    }
                );

                detail.OwnsOne(
                    p => p.ConstType,
                    type =>
                    {
                        type.Property(p => p.ConstType).UseCodeConfig().HasColumnName("ConstType");

                        type.Property(p => p.ConstTypeOther)
                            .UseMaxLengthNVarcharConfig()
                            .HasColumnName("ConstTypeOther");
                    }
                );
            }
        );

        builder.OwnsOne(
            p => p.UtilizationDetail,
            detail =>
            {
                detail.Property(p => p.Utilization).UseCodeConfig().HasColumnName("Utilization");

                detail
                    .Property(p => p.UseForOtherPurpose)
                    .UseMaxLengthNVarcharConfig()
                    .HasColumnName("UseForOtherPurpose");
            }
        );

        builder.Property(p => p.Remark).UseRemarkConfig();

        // BuildingAppraisalSurfaces
        builder.OwnsMany(
            p => p.BuildingAppraisalSurfaces,
            surface =>
            {
                surface.ToTable("BuildingAppraisalSurfaces");
                surface.WithOwner().HasForeignKey("BuildingApprId");

                surface.Property<long>("SurfaceId");
                surface.HasKey("SurfaceId");

                surface.Property(p => p.FromFloorNo).HasColumnName("FromFloorNo");

                surface.Property(p => p.ToFloorNo).HasColumnName("ToFloorNo");

                surface.Property(p => p.FloorType).UseCodeConfig().HasColumnName("FloorType");

                surface
                    .Property(p => p.FloorStructure)
                    .UseCodeConfig()
                    .HasColumnName("FloorStructure");

                surface
                    .Property(p => p.FloorStructureOther)
                    .UseMaxLengthNVarcharConfig()
                    .HasColumnName("FloorStructureOther");

                surface.Property(p => p.FloorSurface).UseCodeConfig().HasColumnName("FloorSurface");

                surface
                    .Property(p => p.FloorSurfaceOther)
                    .UseMaxLengthNVarcharConfig()
                    .HasColumnName("FloorSurfaceOther");
            }
        );

        // BuildingAppraisalDepreciationDetails
        builder.OwnsMany(
            p => p.BuildingAppraisalDepreciationDetails,
            detail =>
            {
                detail.ToTable("BuildingAppraisalDepreciationDetails");
                detail.WithOwner().HasForeignKey("BuildingApprId");

                detail.Property<long>("BuildingDepreciationId");
                detail.HasKey("BuildingDepreciationId");

                detail.Property(p => p.AreaDesc).UseRemarkConfig().HasColumnName("AreaDesc");

                detail.Property(p => p.Area).HasPrecision(5, 3).HasColumnName("Area");

                detail
                    .Property(p => p.PricePerSqM)
                    .UseCompactPriceConfig()
                    .HasColumnName("PricePerSqM");

                detail
                    .Property(p => p.PriceBeforeDegradation)
                    .UseMoneyConfig()
                    .HasColumnName("PriceBeforeDegradation");

                detail.Property(p => p.Year).HasColumnName("Year");

                detail
                    .Property(p => p.DegradationYearPct)
                    .UseMeasurementConfig()
                    .HasColumnName("DegradationYearPct");

                detail
                    .Property(p => p.TotalDegradationPct)
                    .UseMeasurementConfig()
                    .HasColumnName("TotalDegradationPct");

                detail
                    .Property(p => p.PriceDegradation)
                    .UseMoneyConfig()
                    .HasColumnName("PriceDegradation");

                detail.Property(p => p.TotalPrice).UseMoneyConfig().HasColumnName("TotalPrice");

                detail.Property(p => p.AppraisalMethod).HasColumnName("AppraisalMethod");

                // BuildingAppraisalDepreciationPeriods
                detail.OwnsMany(
                    p => p.BuildingAppraisalDepreciationPeriods,
                    period =>
                    {
                        period.ToTable("BuildingAppraisalDepreciationPeriods");
                        period.WithOwner().HasForeignKey("BuildingDepreciationId");

                        period.Property<long>("BuildingDpcPeriodId");
                        period.HasKey("BuildingDpcPeriodId");

                        period.Property(p => p.AtYear).HasColumnName("AtYear");

                        period
                            .Property(p => p.DepreciationPerYear)
                            .UseMoneyConfig()
                            .HasColumnName("DepreciationPerYear");
                    }
                );
            }
        );
    }
}
