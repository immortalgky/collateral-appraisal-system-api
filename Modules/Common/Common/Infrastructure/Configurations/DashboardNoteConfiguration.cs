using Common.Domain.Notes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Infrastructure.Configurations;

public class DashboardNoteConfiguration : IEntityTypeConfiguration<DashboardNote>
{
    public void Configure(EntityTypeBuilder<DashboardNote> builder)
    {
        builder.ToTable("DashboardNotes", "common");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(n => n.UserId)
            .IsRequired();

        builder.Property(n => n.Content)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(n => n.CreatedAt)
            .IsRequired()
            .HasColumnType("datetimeoffset");

        builder.Property(n => n.UpdatedAt)
            .IsRequired()
            .HasColumnType("datetimeoffset");

        builder.HasIndex(n => n.UserId)
            .HasDatabaseName("IX_DashboardNotes_UserId");
    }
}
