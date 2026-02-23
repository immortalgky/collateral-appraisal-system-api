using Document.Domain.UploadSessions.Model;

namespace Document.Data.Configurations;

public class UploadSessionConfiguration : IEntityTypeConfiguration<UploadSession>
{
    public void Configure(EntityTypeBuilder<UploadSession> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Status).HasMaxLength(50);
        builder.Property(p => p.UserAgent).HasMaxLength(500);
        builder.Property(p => p.IpAddress).HasMaxLength(50);
        builder.Property(p => p.ExternalReference).HasMaxLength(256);

        builder
            .HasMany(p => p.Documents)
            .WithOne()
            .HasForeignKey(p => p.UploadSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.Status);
    }
}