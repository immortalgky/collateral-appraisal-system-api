using Request.RequestComments.Models;

namespace Request.Data.Configurations;

public class RequestCommentConfiguration : IEntityTypeConfiguration<RequestComment>
{
    public void Configure(EntityTypeBuilder<RequestComment> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(r => r.RequestId);

        builder.HasIndex(p => p.RequestId);

        builder.Property(r => r.Comment);

        builder.Property(r => r.CommentedBy).HasMaxLength(10);

        builder.Property(r => r.CommentedByName).HasMaxLength(100);

        builder.Property(r => r.CommentedAt);

    }
}