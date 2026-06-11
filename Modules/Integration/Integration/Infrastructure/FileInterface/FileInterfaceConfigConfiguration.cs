using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Integration.Infrastructure.FileInterface;

public class FileInterfaceConfigConfiguration : IEntityTypeConfiguration<FileInterfaceConfigEntity>
{
    public void Configure(EntityTypeBuilder<FileInterfaceConfigEntity> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.InterfaceCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Direction)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.FileNamePrefix).HasMaxLength(100);
        builder.Property(x => x.FileNameDateFormat).HasMaxLength(50);
        builder.Property(x => x.FileExtension).HasMaxLength(20);
        builder.Property(x => x.Directory).HasMaxLength(500);
        builder.Property(x => x.ProcessedDirectory).HasMaxLength(500);
        builder.Property(x => x.FilePattern).HasMaxLength(200);

        builder.HasIndex(x => x.InterfaceCode)
            .IsUnique()
            .HasDatabaseName("IX_FileInterfaceConfigs_InterfaceCode");
    }
}
