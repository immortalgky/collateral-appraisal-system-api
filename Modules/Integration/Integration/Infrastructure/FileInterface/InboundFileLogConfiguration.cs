using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Integration.Infrastructure.FileInterface;

public class InboundFileLogConfiguration : IEntityTypeConfiguration<InboundFileLog>
{
    public void Configure(EntityTypeBuilder<InboundFileLog> builder)
    {
        builder.ToTable("InboundFileLogs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.InterfaceCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.ContentHash).HasMaxLength(64);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // The de-duplication key. Content hash is part of it so a file re-sent under the same name
        // with corrected content is treated as new work rather than silently skipped.
        builder.HasIndex(x => new { x.InterfaceCode, x.FileName, x.ContentHash })
            .IsUnique()
            .HasDatabaseName("UX_InboundFileLogs_Interface_File_Hash");

        // Serves the cheap first-pass check (interface + name + size) that runs before any download.
        builder.HasIndex(x => new { x.InterfaceCode, x.FileName, x.SizeBytes })
            .HasDatabaseName("IX_InboundFileLogs_Interface_File_Size");

        // "when did the last file for this interface land" — for the monitoring endpoint.
        builder.HasIndex(x => new { x.InterfaceCode, x.CompletedAt })
            .HasDatabaseName("IX_InboundFileLogs_Interface_CompletedAt");
    }
}
