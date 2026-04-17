using Common.Domain.SavedSearches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Infrastructure.Configurations;

public class SavedSearchConfiguration : IEntityTypeConfiguration<SavedSearch>
{
    public void Configure(EntityTypeBuilder<SavedSearch> builder)
    {
        builder.ToTable("SavedSearches", "common");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(s => s.UserId)
            .IsRequired();

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(SavedSearch.MaxNameLength);

        builder.Property(s => s.EntityType)
            .IsRequired()
            .HasMaxLength(SavedSearch.MaxEntityTypeLength);

        builder.Property(s => s.FiltersJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(s => s.SortBy)
            .HasMaxLength(SavedSearch.MaxSortByLength);

        builder.Property(s => s.SortDir)
            .HasMaxLength(SavedSearch.MaxSortDirLength);

        builder.Property(s => s.CreatedAt)
            .IsRequired()
            .HasColumnType("datetimeoffset");

        builder.Property(s => s.UpdatedAt)
            .IsRequired()
            .HasColumnType("datetimeoffset");

        builder.HasIndex(s => s.UserId)
            .HasDatabaseName("IX_SavedSearches_UserId");
    }
}
