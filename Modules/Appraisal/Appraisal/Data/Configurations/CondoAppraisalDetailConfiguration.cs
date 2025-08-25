using Appraisal.AppraisalProperties.Models;

namespace Appraisal.Data.Configurations;

public class CondoAppraisalDetailConfiguration : IEntityTypeConfiguration<CondoAppraisalDetail>
{
    public void Configure(EntityTypeBuilder<CondoAppraisalDetail> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).UseIdentityColumn().HasColumnName("CondoApprId");

        builder
            .HasOne<RequestAppraisal>()
            .WithOne(p => p.CondoAppraisalDetail)
            .HasForeignKey<CondoAppraisalDetail>(p => p.ApprId);

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

        builder.Property(p => p.DocValidate).UseCodeConfig().HasColumnName("DocValidate");

        builder.OwnsOne(
            p => p.CondominiumLocation,
            location =>
            {
                location.Property(p => p.CondoLocation).HasColumnName("CondoLocation");

                location.Property(p => p.Street).UseStreetConfig().HasColumnName("Street");

                location.Property(p => p.Soi).UseSoiConfig().HasColumnName("Soi");

                location.Property(p => p.Distance).UseDistanceConfig().HasColumnName("Distance");

                location
                    .Property(p => p.RoadWidth)
                    .UseRoadWidthConfig()
                    .HasColumnName("RoadWidth");

                location
                    .Property(p => p.RightOfWay)
                    .UseRightOfWayConfig()
                    .HasColumnName("RightOfWay");

                location.Property(p => p.RoadSurface).UseCodeConfig().HasColumnName("RoadSurface");

                location
                    .Property(p => p.PublicUtility)
                    .UseLongStringConfig()
                    .HasColumnName("PublicUtility");

                location
                    .Property(p => p.PublicUtilityOther)
                    .UseMaxLengthNVarcharConfig()
                    .HasColumnName("PublicUtilityOther");
            }
        );

        builder.OwnsOne(
            p => p.CondoAttribute,
            attribute =>
            {
                attribute.Property(p => p.Decoration).UseCodeConfig().HasColumnName("Decoration");

                attribute.Property(p => p.BuildingYear).HasColumnName("BuildingYear");

                attribute.Property(p => p.CondoHeight).HasColumnName("CondoHeight");

                attribute
                    .Property(p => p.BuildingForm)
                    .UseCodeConfig()
                    .HasColumnName("BuildingForm");

                attribute
                    .Property(p => p.ConstMaterial)
                    .UseCodeConfig()
                    .HasColumnName("ConstMaterial");

                attribute.OwnsOne(
                    p => p.CondoRoomLayout,
                    layout =>
                    {
                        layout
                            .Property(p => p.RoomLayout)
                            .UseCodeConfig()
                            .HasColumnName("RoomLayout");

                        layout
                            .Property(p => p.RoomLayoutOther)
                            .UseMaxLengthNVarcharConfig()
                            .HasColumnName("RoomLayoutOther");
                    }
                );

                attribute.OwnsOne(
                    p => p.CondoFloor,
                    floor =>
                    {
                        floor
                            .Property(p => p.GroundFloorMaterial)
                            .UseCodeConfig()
                            .HasColumnName("GroundFloorMaterial");

                        floor
                            .Property(p => p.GroundFloorMaterialOther)
                            .UseMaxLengthNVarcharConfig()
                            .HasColumnName("GroundFloorMaterialOther");

                        floor
                            .Property(p => p.UpperFloorMaterial)
                            .UseCodeConfig()
                            .HasColumnName("UpperFloorMaterial");

                        floor
                            .Property(p => p.UpperFloorMaterialOther)
                            .UseMaxLengthNVarcharConfig()
                            .HasColumnName("UpperFloorMaterialOther");

                        floor
                            .Property(p => p.BathroomFloorMaterial)
                            .UseCodeConfig()
                            .HasColumnName("BathroomFloorMaterial");

                        floor
                            .Property(p => p.BathroomFloorMaterialOther)
                            .UseMaxLengthNVarcharConfig()
                            .HasColumnName("BathroomFloorMaterialOther");
                    }
                );

                attribute.OwnsOne(
                    p => p.CondoRoof,
                    roof =>
                    {
                        roof.Property(p => p.Roof).UseCodeConfig().HasColumnName("Roof");

                        roof.Property(p => p.RoofOther)
                            .UseMaxLengthNVarcharConfig()
                            .HasColumnName("RoofOther");
                    }
                );

                attribute
                    .Property(p => p.TotalAreaInSqM)
                    .UseLargeAreaConfig()
                    .HasColumnName("TotalAreaInSqM");
            }
        );

        builder.OwnsOne(
            p => p.Expropriation,
            expropriation =>
            {
                expropriation.Property(p => p.IsExpropriate).HasColumnName("IsExpropriate");

                expropriation
                    .Property(p => p.IsExpropriateRemark)
                    .UseRemarkConfig()
                    .HasColumnName("IsExpropriateRemark");

                expropriation.Property(p => p.InLineExpropriate).HasColumnName("InLineExpropriate");

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

        builder.OwnsOne(
            p => p.CondominiumFacility,
            facility =>
            {
                facility
                    .Property(p => p.CondoFacility)
                    .UseLongStringConfig()
                    .HasColumnName("CondoFacility");

                facility
                    .Property(p => p.CondoFacilityOther)
                    .UseMaxLengthNVarcharConfig()
                    .HasColumnName("CondoFacilityOther");
            }
        );

        builder.OwnsOne(
            p => p.CondoPrice,
            price =>
            {
                price
                    .Property(p => p.BuildingInsurancePrice)
                    .UseMoneyConfig()
                    .HasColumnName("BuildingInsurancePrice");

                price.Property(p => p.SellingPrice).UseMoneyConfig().HasColumnName("SellingPrice");

                price
                    .Property(p => p.ForceSellingPrice)
                    .UseMoneyConfig()
                    .HasColumnName("ForceSellingPrice");
            }
        );

        builder.OwnsOne(
            p => p.ForestBoundary,
            boundary =>
            {
                boundary.Property(p => p.IsForestBoundary).HasColumnName("IsForestBoundary");

                boundary
                    .Property(p => p.IsForestBoundaryRemark)
                    .UseRemarkConfig()
                    .HasColumnName("IsForestBoundaryRemark");
            }
        );

        builder.Property(p => p.Remark).UseRemarkConfig().HasColumnName("Remark");

        // CondoAppraisalAreaDetails
        builder.OwnsMany(
            p => p.CondoAppraisalAreaDetails,
            detail =>
            {
                detail.ToTable("CondoAppraisalAreaDetails");
                detail.WithOwner().HasForeignKey("CondoApprId");

                detail.Property<long>("CondoAreaDetId");
                detail.HasKey("CondoAreaDetId");

                detail.Property(p => p.AreaDesc).UseRemarkConfig().HasColumnName("AreaDesc");

                detail.Property(p => p.AreaSize).UseAreaConfig().HasColumnName("AreaSize");
            }
        );
    }
}
