using Appraisal.Domain.Appraisals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Appraisal.Infrastructure.Configurations;

public class AppendixTypeConfiguration : IEntityTypeConfiguration<AppendixType>
{
    public void Configure(EntityTypeBuilder<AppendixType> builder)
    {
        builder.ToTable("AppendixTypes");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(a => a.Code).IsRequired().HasMaxLength(30);
        builder.Property(a => a.Name).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Description).HasMaxLength(500);
        builder.Property(a => a.SortOrder).IsRequired();
        builder.Property(a => a.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasIndex(a => a.Code).IsUnique();
    }
}

public class AppraisalAppendixConfiguration : IEntityTypeConfiguration<AppraisalAppendix>
{
    public void Configure(EntityTypeBuilder<AppraisalAppendix> builder)
    {
        builder.ToTable("AppraisalAppendices");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(a => a.AppraisalId).IsRequired();
        builder.Property(a => a.AppendixTypeId).IsRequired();
        builder.Property(a => a.SortOrder).IsRequired();
        builder.Property(a => a.LayoutColumns).IsRequired().HasDefaultValue(1);

        builder.HasMany(a => a.Documents)
            .WithOne()
            .HasForeignKey(d => d.AppraisalAppendixId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.AppraisalId);
        builder.HasIndex(a => new { a.AppraisalId, a.AppendixTypeId }).IsUnique();
    }
}

public class AppendixDocumentConfiguration : IEntityTypeConfiguration<AppendixDocument>
{
    public void Configure(EntityTypeBuilder<AppendixDocument> builder)
    {
        builder.ToTable("AppendixDocuments");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(d => d.AppraisalAppendixId).IsRequired();
        builder.Property(d => d.GalleryPhotoId).IsRequired();
        builder.Property(d => d.DisplaySequence).IsRequired();

        builder.HasIndex(d => d.AppraisalAppendixId);
    }
}
