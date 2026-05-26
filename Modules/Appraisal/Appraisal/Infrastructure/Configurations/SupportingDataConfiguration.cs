namespace Appraisal.Infrastructure.Configurations;

public class SupportingDataConfiguration : IEntityTypeConfiguration<SupportingData>
{
    public void Configure(EntityTypeBuilder<SupportingData> builder)
    {
        builder.ToTable("SupportingData");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.OwnsOne(x => x.SupportingNumber, sn =>
        {
            sn.Property(p => p.Value).HasMaxLength(50).HasColumnName("SupportingNumber");
            sn.HasIndex(p => p.Value).HasDatabaseName("IX_SupportingData_SupportingNumber");
        });

        builder.Property(x => x.ImportChannel).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ImportDate).IsRequired();
        builder.Property(x => x.SourceOfData).HasMaxLength(20).IsRequired();
        builder.Property(x => x.AppraisalCompany).HasMaxLength(50);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Remark).HasMaxLength(4000);

        builder.Property(x => x.Status)
            .HasConversion(v => v.Code, v => SupportingStatus.FromString(v))
            .HasMaxLength(20)
            .HasColumnName("Status");

        builder.HasMany(x => x.Details)
            .WithOne()
            .HasForeignKey(d => d.SupportingDataId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Status).HasDatabaseName("IX_SupportingData_Status");
        builder.HasIndex(x => x.ImportDate).IsDescending(true)
            .HasDatabaseName("IX_SupportingData_ImportDate");
    }
}

public class SupportingDataDetailConfiguration : IEntityTypeConfiguration<SupportingDataDetail>
{
    public void Configure(EntityTypeBuilder<SupportingDataDetail> builder)
    {
        builder.ToTable("SupportingDataDetails");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.SupportingDataId).IsRequired();

        builder.Property(x => x.PropertyName).HasMaxLength(200);
        builder.Property(x => x.Developer).HasMaxLength(200);
        builder.Property(x => x.ModelName).HasMaxLength(200);
        builder.Property(x => x.CollateralType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.BuildingType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.LandArea).HasPrecision(18, 4);
        builder.Property(x => x.UsableArea).HasPrecision(18, 4);
        builder.Property(x => x.ProjectName).HasMaxLength(200);
        builder.Property(x => x.RoomFloor).HasMaxLength(50);

        builder.OwnsOne(x => x.Address, addr =>
        {
            addr.Property(a => a.HouseNo).HasMaxLength(30).HasColumnName("HouseNo");
            addr.Property(a => a.SubDistrict).HasMaxLength(10).HasColumnName("SubDistrict");
            addr.Property(a => a.District).HasMaxLength(10).HasColumnName("District");
            addr.Property(a => a.Province).HasMaxLength(10).HasColumnName("Province");
        });

        builder.OwnsOne(x => x.Location, loc =>
        {
            loc.Property(l => l.Latitude).HasPrecision(10, 7).HasColumnName("Latitude");
            loc.Property(l => l.Longitude).HasPrecision(10, 7).HasColumnName("Longitude");
        });

        builder.Property(x => x.PlotLocationType).HasMaxLength(20);
        builder.Property(x => x.PricePerUnit).HasPrecision(19, 4);
        builder.Property(x => x.OfferingPrice).HasPrecision(19, 4);
        builder.Property(x => x.SellingPrice).HasPrecision(19, 4);
        builder.Property(x => x.PhoneNo).HasMaxLength(50);
        builder.Property(x => x.InformationDate).IsRequired();
        builder.Property(x => x.Website).HasMaxLength(500);
        builder.Property(x => x.SourceUrl).HasMaxLength(1000);
        builder.Property(x => x.Remark).HasMaxLength(4000);

        builder.HasIndex(x => x.SupportingDataId).HasDatabaseName("IX_SupportingDataDetails_SupportingDataId");
        builder.HasIndex(x => x.CollateralType).HasDatabaseName("IX_SupportingDataDetails_CollateralType");
    }
}