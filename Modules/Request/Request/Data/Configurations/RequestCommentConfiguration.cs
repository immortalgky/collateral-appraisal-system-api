using Request.RequestComments.Models;

namespace Request.Data.Configurations;

public class RequestCommentConfiguration : IEntityTypeConfiguration<RequestComment>
{
    public void Configure(EntityTypeBuilder<RequestComment> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        builder.Property(r => r.RequestId);

        builder.Property(r => r.Comment);

        builder.Property(r => r.CommentedBy).HasMaxLength(10);

        builder.Property(r => r.CommentedByName).HasMaxLength(100);

        builder.Property(r => r.CommentedAt);

        builder.HasOne(r => r.Request)
            .WithMany(r => r.RequestComments)
            .HasForeignKey(r => r.RequestId)
            .HasConstraintName("FK_RequestComment_Request");

        builder.HasIndex(p => p.RequestId);
    }
}