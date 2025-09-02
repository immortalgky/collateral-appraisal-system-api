using Collateral.CollateralProperties.Models;

namespace Collateral.Data.Configurations;

public class LandTitleConfiguration : IEntityTypeConfiguration<LandTitle>
{
    public void Configure(EntityTypeBuilder<LandTitle> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).UseIdentityColumn().HasColumnName("LandTitleId");

        builder.OwnsOne(
            p => p.LandTitleDocumentDetail,
            detail =>
            {
                detail.Property(p => p.TitleNo).UseTinyStringConfig().HasColumnName("TitleNo");

                detail.Property(p => p.BookNo).UseTinyStringConfig().HasColumnName("BookNo");

                detail.Property(p => p.PageNo).UseTinyStringConfig().HasColumnName("PageNo");

                detail.Property(p => p.LandNo).UseTinyStringConfig().HasColumnName("LandNo");

                detail.Property(p => p.SurveyNo).UseTinyStringConfig().HasColumnName("SurveyNo");

                detail.Property(p => p.SheetNo).UseTinyStringConfig().HasColumnName("SheetNo");
            }
        );

        builder.OwnsOne(
            p => p.LandTitleArea,
            area =>
            {
                area.Property(p => p.Rai).UseRaiConfig().HasColumnName("Rai");

                area.Property(p => p.Ngan).UseNganConfig().HasColumnName("Ngan");

                area.Property(p => p.Wa).UseWaConfig().HasColumnName("Wa");

                area.Property(p => p.TotalAreaInSqWa)
                    .HasPrecision(15, 2)
                    .HasColumnName("TotalAreaInSqWa");
            }
        );

        builder.Property(p => p.DocumentType).UseCodeConfig();

        builder.Property(p => p.Rawang).UseVeryShortStringConfig();

        builder.Property(p => p.AerialPhotoNo).UseVeryShortStringConfig();

        builder.Property(p => p.BoundaryMarker).UseCodeConfig();

        builder.Property(p => p.BoundaryMarkerOther).UseMaxLengthNVarcharConfig();

        builder.Property(p => p.DocValidate).UseCodeConfig();

        builder.Property(p => p.PricePerSquareWa).HasPrecision(10, 4);

        builder.Property(p => p.GovernmentPrice).UseMoneyConfig();
    }
}
