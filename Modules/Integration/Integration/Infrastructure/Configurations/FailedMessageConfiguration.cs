using System.Text.Json;
using Integration.Domain.FailedMessages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Integration.Infrastructure.Configurations;

public class FailedMessageConfiguration : IEntityTypeConfiguration<FailedMessage>
{
    // The collector truncates every broker-controlled string to these same
    // constants before INSERT, so a value that can never fit the column can never block the queue
    // head (Integration.FailedMessages.FailedMessageCollectorService.Truncate*).
    public const int NodeMaxLength = 100;
    // RabbitMQ queue names can be up to 255 BYTES; queue names are ASCII in practice, so 255 chars
    // covers the real maximum — the collector's TruncateForColumn call is kept as a defensive guard even
    // though a real queue name can never actually hit it.
    public const int SourceQueueMaxLength = 255;
    public const int KindMaxLength = 20;
    public const int MessageTypeMaxLength = 500;
    public const int ConsumerTypeMaxLength = 500;
    public const int ExceptionTypeMaxLength = 300;
    public const int ExceptionMessageMaxLength = 2000;
    public const int RefTypeMaxLength = 20;
    public const int RefNumberMaxLength = 50;
    public const int ContentTypeMaxLength = 200;
    public const int StatusMaxLength = 20;
    public const int ActionByMaxLength = 50;
    public const int ActionReasonMaxLength = 500;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Silences EF's "collection with a value converter but no value comparer" startup warning: without
    // this, EF falls back to reference equality, so Headers looks "changed" (and gets rewritten) even
    // when nothing actually did. Equality is by key/value set (order doesn't matter), the hash is
    // order-independent (XOR) to stay consistent with that equality, and the snapshot is a defensive
    // copy so later mutation of the entity's own dictionary can't corrupt EF's change-tracking snapshot.
    private static readonly ValueComparer<Dictionary<string, string>> HeadersComparer = new(
        (a, b) => (a ?? new Dictionary<string, string>()).OrderBy(kv => kv.Key)
            .SequenceEqual((b ?? new Dictionary<string, string>()).OrderBy(kv => kv.Key)),
        d => d.Aggregate(0, (hash, kv) => hash ^ HashCode.Combine(kv.Key, kv.Value)),
        d => new Dictionary<string, string>(d));

    public void Configure(EntityTypeBuilder<FailedMessage> builder)
    {
        builder.ToTable("FailedMessages");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Node).HasMaxLength(NodeMaxLength).IsRequired();
        builder.Property(x => x.SourceQueue).HasMaxLength(SourceQueueMaxLength).IsRequired();
        builder.Property(x => x.Kind).HasMaxLength(KindMaxLength).IsRequired();
        builder.Property(x => x.MessageType).HasMaxLength(MessageTypeMaxLength).IsRequired();
        builder.Property(x => x.ConsumerType).HasMaxLength(ConsumerTypeMaxLength);
        builder.Property(x => x.ExceptionType).HasMaxLength(ExceptionTypeMaxLength).IsRequired();
        builder.Property(x => x.ExceptionMessage).HasMaxLength(ExceptionMessageMaxLength).IsRequired();
        builder.Property(x => x.StackTrace).HasColumnType("nvarchar(max)");
        builder.Property(x => x.RefType).HasMaxLength(RefTypeMaxLength);
        builder.Property(x => x.RefNumber).HasMaxLength(RefNumberMaxLength);
        builder.Property(x => x.ContentType).HasMaxLength(ContentTypeMaxLength);
        builder.Property(x => x.Status).HasMaxLength(StatusMaxLength).IsRequired();
        builder.Property(x => x.ActionBy).HasMaxLength(ActionByMaxLength);
        builder.Property(x => x.ActionReason).HasMaxLength(ActionReasonMaxLength);
        builder.Property(x => x.RetryClaimedAt).HasColumnType("datetime2");

        builder.Property(x => x.Body)
            .HasColumnType("varbinary(max)")
            .IsRequired();

        builder.Property(x => x.Headers)
            .HasConversion(
                v => JsonSerializer.Serialize(v, SerializerOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, SerializerOptions)
                     ?? new Dictionary<string, string>())
            .HasColumnType("nvarchar(max)")
            .Metadata.SetValueComparer(HeadersComparer);

        builder.Property(x => x.RowVersion).IsRowVersion();

        // Design D11: one event can fail at several consumers, and a retried message keeps its
        // MessageId, so the dedup key needs all four columns. Filtered because collected rows
        // without a resolvable MessageId (malformed envelope) must not collide with each other.
        builder.HasIndex(x => new { x.MessageId, x.SourceQueue, x.Kind, x.FaultedAt })
            .IsUnique()
            .HasFilter("[MessageId] IS NOT NULL")
            .HasDatabaseName("UX_FailedMessages_Dedup");

        builder.HasIndex(x => new { x.Status, x.FaultedAt })
            .HasDatabaseName("IX_FailedMessages_Status_FaultedAt");

        builder.HasIndex(x => new { x.Node, x.Status })
            .HasDatabaseName("IX_FailedMessages_Node_Status");
    }
}
