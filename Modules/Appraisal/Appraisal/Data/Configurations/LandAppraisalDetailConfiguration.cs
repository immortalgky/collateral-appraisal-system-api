using Appraisal.AppraisalProperties.Models;

namespace Appraisal.Data.Configurations;

public class LandAppraisalDetailConfiguration : IEntityTypeConfiguration<LandAppraisalDetail>
{
    public void Configure(EntityTypeBuilder<LandAppraisalDetail> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).UseIdentityColumn().HasColumnName("LandApprId");

        builder
            .HasOne<RequestAppraisal>()
            .WithOne(p => p.LandAppraisalDetail)
            .HasForeignKey<LandAppraisalDetail>(p => p.ApprId);

        builder.Property(p => p.PropertyName).UseLongStringConfig();

        builder.Property(p => p.CheckOwner).UseCodeConfig();

        builder.Property(p => p.Owner).UseLongStringConfig();

        builder.OwnsOne(
            p => p.ObligationDetail,
            detail =>
            {
                detail.Property(p => p.IsObligation).UseCodeConfig().HasColumnName("IsObligation");

                detail
                    .Property(p => p.Obligation)
                    .UseObligationConfig()
                    .HasColumnName("Obligation");
            }
        );

        builder.OwnsOne(
            p => p.LandLocationDetail,
            location =>
            {
                location
                    .Property(p => p.LandLocation)
                    .UseCodeConfig()
                    .HasColumnName("LandLocation");

                location.Property(p => p.LandCheck).UseCodeConfig().HasColumnName("LandCheck");

                location
                    .Property(p => p.LandCheckOther)
                    .UseOtherFieldConfig()
                    .HasColumnName("LandCheckOther");

                location.Property(p => p.Street).UseStreetConfig().HasColumnName("Street");

                location.Property(p => p.Soi).UseSoiConfig().HasColumnName("Soi");

                location.Property(p => p.Distance).UseDistanceConfig().HasColumnName("Distance");

                location.Property(p => p.Village).UseVillageConfig().HasColumnName("Village");

                location
                    .Property(p => p.AddressLocation)
                    .UseLongStringConfig()
                    .HasColumnName("AddressLocation");

                location.Property(p => p.LandShape).UseCodeConfig().HasColumnName("LandShape");

                location
                    .Property(p => p.UrbanPlanningType)
                    .UseCodeConfig()
                    .HasColumnName("UrbanPlanningType");

                location.Property(p => p.Location).UseMultiSelectConfig().HasColumnName("Location");

                location
                    .Property(p => p.PlotLocation)
                    .UseCodeConfig()
                    .HasColumnName("PlotLocation");

                location
                    .Property(p => p.PlotLocationOther)
                    .UseOtherFieldConfig()
                    .HasColumnName("PlotLocationOther");
            }
        );

        builder.OwnsOne(
            p => p.LandFillDetail,
            fill =>
            {
                fill.Property(p => p.LandFill).UseMultiSelectConfig().HasColumnName("LandFill");

                fill.Property(p => p.LandFillPct)
                    .UseMeasurementConfig()
                    .HasColumnName("LandFillPct");

                fill.Property(p => p.SoilLevel).UseMeasurementConfig().HasColumnName("SoilLevel");
            }
        );

        builder.OwnsOne(
            p => p.LandAccessibilityDetail,
            detail =>
            {
                detail.OwnsOne(
                    p => p.FrontageRoad,
                    road =>
                    {
                        road.Property(p => p.RoadWidth)
                            .UseRoadWidthConfig()
                            .HasColumnName("RoadWidth");

                        road.Property(p => p.RightOfWay)
                            .UseRightOfWayConfig()
                            .HasColumnName("RightOfWay");

                        road.Property(p => p.WideFrontageOfLand)
                            .HasPrecision(10, 2)
                            .HasColumnName("WideFrontageOfLand");

                        road.Property(p => p.NoOfSideFacingRoad)
                            .HasPrecision(10, 2)
                            .HasColumnName("NoOfSideFacingRoad");

                        road.Property(p => p.RoadPassInFrontOfLand)
                            .HasPrecision(10, 2)
                            .HasColumnName("RoadPassInFrontOfLand");

                        road.Property(p => p.LandAccessibility)
                            .UseCodeConfig()
                            .HasColumnName("LandAccessibility");

                        road.Property(p => p.LandAccessibilityDesc)
                            .UseRemarkConfig()
                            .HasColumnName("LandAccessibilityDesc");
                    }
                );

                detail.Property(p => p.RoadSurface).UseCodeConfig().HasColumnName("RoadSurface");

                detail
                    .Property(p => p.RoadSurfaceOther)
                    .UseOtherFieldConfig()
                    .HasColumnName("RoadSurfaceOther");

                detail
                    .Property(p => p.PublicUtility)
                    .UseMultiSelectConfig()
                    .HasColumnName("PublicUtility");

                detail
                    .Property(p => p.PublicUtilityOther)
                    .UseOtherFieldConfig()
                    .HasColumnName("PublicUtilityOther");

                detail.Property(p => p.LandUse).UseMultiSelectConfig().HasColumnName("LandUse");

                detail
                    .Property(p => p.LandUseOther)
                    .UseOtherFieldConfig()
                    .HasColumnName("LandUseOther");

                detail
                    .Property(p => p.LandEntranceExit)
                    .UseMultiSelectConfig()
                    .HasColumnName("LandEntranceExit");

                detail
                    .Property(p => p.LandEntranceExitOther)
                    .UseOtherFieldConfig()
                    .HasColumnName("LandEntranceExitOther");

                detail
                    .Property(p => p.Transportation)
                    .UseMultiSelectConfig()
                    .HasColumnName("Transportation");

                detail
                    .Property(p => p.TransportationOther)
                    .UseOtherFieldConfig()
                    .HasColumnName("TransportationOther");
            }
        );

        builder.Property(p => p.AnticipationOfProp).UseMultiSelectConfig();

        builder.OwnsOne(
            p => p.LandLimitation,
            limitation =>
            {
                limitation.OwnsOne(
                    p => p.Expropriation,
                    expropriation =>
                    {
                        expropriation.Property(p => p.IsExpropriate).HasColumnName("IsExpropriate");

                        expropriation
                            .Property(p => p.IsExpropriateRemark)
                            .UseRemarkConfig()
                            .HasColumnName("IsExpropriateRemark");

                        expropriation
                            .Property(p => p.InLineExpropriate)
                            .HasColumnName("InLineExpropriate");

                        expropriation
                            .Property(p => p.InLineExpropriateRemark)
                            .UseRemarkConfig()
                            .HasColumnName("InLineExpropriateRemark");

                        expropriation
                            .Property(p => p.RoyalDecree)
                            .UseRoyalDecreeConfig()
                            .HasColumnName("RoyalDecree");
                    }
                );

                limitation.OwnsOne(
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
                            .UseMeasurementConfig()
                            .HasColumnName("EncroachArea");
                    }
                );

                limitation
                    .Property(p => p.Electricity)
                    .UseCodeConfig()
                    .HasColumnName("Electricity");

                limitation
                    .Property(p => p.ElectricityDistance)
                    .UseMeasurementConfig()
                    .HasColumnName("ElectricityDistance");

                limitation.Property(p => p.IsLandlocked).HasColumnName("IsLandlocked");

                limitation
                    .Property(p => p.IsLandlockedRemark)
                    .UseRemarkConfig()
                    .HasColumnName("IsLandlockedRemark");

                limitation.OwnsOne(
                    p => p.ForestBoundary,
                    boundary =>
                    {
                        boundary
                            .Property(p => p.IsForestBoundary)
                            .HasColumnName("IsForestBoundary");

                        boundary
                            .Property(p => p.IsForestBoundaryRemark)
                            .UseRemarkConfig()
                            .HasColumnName("IsForestBoundaryRemark");
                    }
                );

                limitation
                    .Property(p => p.LimitationOther)
                    .UseOtherFieldConfig()
                    .HasColumnName("LimitationOther");
            }
        );

        builder.Property(p => p.Eviction).UseMultiSelectConfig();

        builder.Property(p => p.Allocation).UseCodeConfig();

        builder.OwnsOne(
            p => p.ConsecutiveArea,
            area =>
            {
                area.Property(p => p.NConsecutiveArea)
                    .UseLongerStringConfig()
                    .HasColumnName("N_ConsecutiveArea");

                area.Property(p => p.NEstimateLength)
                    .UseMeasurementConfig()
                    .HasColumnName("N_EstimateLength");

                area.Property(p => p.SConsecutiveArea)
                    .UseLongerStringConfig()
                    .HasColumnName("S_ConsecutiveArea");

                area.Property(p => p.SEstimateLength)
                    .UseMeasurementConfig()
                    .HasColumnName("S_EstimateLength");

                area.Property(p => p.EConsecutiveArea)
                    .UseLongerStringConfig()
                    .HasColumnName("E_ConsecutiveArea");

                area.Property(p => p.EEstimateLength)
                    .UseMeasurementConfig()
                    .HasColumnName("E_EstimateLength");

                area.Property(p => p.WConsecutiveArea)
                    .UseLongerStringConfig()
                    .HasColumnName("W_ConsecutiveArea");

                area.Property(p => p.WEstimateLength)
                    .UseMeasurementConfig()
                    .HasColumnName("W_EstimateLength");
            }
        );

        builder.OwnsOne(
            p => p.LandMiscellaneousDetail,
            detail =>
            {
                detail.Property(p => p.PondArea).UseMeasurementConfig().HasColumnName("PondArea");

                detail.Property(p => p.DepthPit).UseMeasurementConfig().HasColumnName("DepthPit");

                detail.Property(p => p.HasBuilding).UseCodeConfig().HasColumnName("HasBuilding");

                detail
                    .Property(p => p.HasBuildingOther)
                    .UseOtherFieldConfig()
                    .HasColumnName("HasBuildingOther");
            }
        );
    }
}
