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
            // Supporting numbers: format SUP-{000001}-{YYYY} e.g. "SUP-000001-2569"
            sn.Property(p => p.Value).HasMaxLength(15).HasColumnName("SupportingNumber");
            sn.HasIndex(p => p.Value).HasDatabaseName("IX_SupportingData_SupportingNumber");
        });

        builder.Property(x => x.ImportChannel).HasMaxLength(2);
        builder.Property(x => x.ImportDate);
        builder.Property(x => x.SourceOfData).HasMaxLength(2);
        builder.Property(x => x.AppraisalCompanyId);
        builder.Property(x => x.Description).HasMaxLength(100);
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

        builder.Property(x => x.PropertyName).HasMaxLength(100);
        builder.Property(x => x.Developer).HasMaxLength(50);
        builder.Property(x => x.ModelName).HasMaxLength(50);
        builder.Property(x => x.CollateralType).HasMaxLength(3);
        builder.Property(x => x.BuildingType).HasMaxLength(2);
        builder.Property(x => x.LandArea).HasPrecision(17, 2);
        builder.Property(x => x.UsableArea).HasPrecision(17, 2);
        builder.Property(x => x.ProjectName).HasMaxLength(100);
        builder.Property(x => x.RoomFloor).HasMaxLength(3);

        builder.OwnsOne(x => x.Address, addr =>
        {
            addr.Property(a => a.HouseNo).HasMaxLength(30).HasColumnName("HouseNo");
            addr.Property(a => a.SubDistrict).HasMaxLength(100).HasColumnName("SubDistrict");
            addr.Property(a => a.District).HasMaxLength(100).HasColumnName("District");
            addr.Property(a => a.Province).HasMaxLength(100).HasColumnName("Province");
        });

        builder.OwnsOne(x => x.Location, loc =>
        {
            loc.Property(l => l.Latitude).HasPrecision(9, 6).HasColumnName("Latitude");
            loc.Property(l => l.Longitude).HasPrecision(9, 6).HasColumnName("Longitude");
        });

        builder.Property(x => x.PlotLocationType).HasMaxLength(100);
        builder.Property(x => x.PlotLocationTypeOther).HasMaxLength(1000);
        builder.Property(x => x.PricePerUnit).HasPrecision(17, 2);
        builder.Property(x => x.OfferingPrice).HasPrecision(17, 2);
        builder.Property(x => x.SellingPrice).HasPrecision(17, 2);
        builder.Property(x => x.PhoneNo).HasMaxLength(20);
        builder.Property(x => x.InformationDate);
        builder.Property(x => x.Website).HasMaxLength(100);
        builder.Property(x => x.SourceUrl).HasMaxLength(1000);
        builder.Property(x => x.Remark).HasMaxLength(4000);

        builder.HasMany(x => x.Images)
            .WithOne()
            .HasForeignKey(i => i.SupportingDataDetailId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.SupportingDataId).HasDatabaseName("IX_SupportingDataDetails_SupportingDataId");
        builder.HasIndex(x => x.CollateralType).HasDatabaseName("IX_SupportingDataDetails_CollateralType");
    }
}

public class SupportingDataDetailImageConfiguration : IEntityTypeConfiguration<SupportingDataDetailImage>
{
    public void Configure(EntityTypeBuilder<SupportingDataDetailImage> builder)
    {
        builder.ToTable("SupportingDataDetailImages");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.SupportingDataDetailId).IsRequired();
        builder.Property(i => i.DocumentId).IsRequired();
        builder.Property(i => i.StorageUrl).IsRequired().HasMaxLength(2000);
        builder.Property(i => i.FileName).HasMaxLength(500);
        builder.Property(i => i.Title).HasMaxLength(200);
        builder.Property(i => i.Description).HasMaxLength(500);
        builder.Property(i => i.DisplaySequence).IsRequired();

        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.CreatedBy).IsRequired();
        builder.Ignore(i => i.UpdatedAt);
        builder.Ignore(i => i.UpdatedBy);

        builder.HasIndex(i => i.SupportingDataDetailId)
            .HasDatabaseName("IX_SupportingDataDetailImages_DetailId");
        builder.HasIndex(i => new { i.SupportingDataDetailId, i.DisplaySequence })
            .HasDatabaseName("IX_SupportingDataDetailImages_DetailId_Sequence");
    }
}