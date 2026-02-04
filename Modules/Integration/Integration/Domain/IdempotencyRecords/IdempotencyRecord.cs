namespace Integration.Domain.IdempotencyRecords;

public class IdempotencyRecord : Entity<Guid>
{
    public string IdempotencyKey { get; private set; } = default!;
    public string OperationType { get; private set; } = default!;
    public string? RequestHash { get; private set; }
    public string? ResponseData { get; private set; }
    public int StatusCode { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private IdempotencyRecord()
    {
    }

    private IdempotencyRecord(
        string idempotencyKey,
        string operationType,
        string? requestHash,
        DateTime expiresAt)
    {
        Id = Guid.NewGuid();
        IdempotencyKey = idempotencyKey;
        OperationType = operationType;
        RequestHash = requestHash;
        ExpiresAt = expiresAt;
        StatusCode = 0;
    }

    public static IdempotencyRecord Create(
        string idempotencyKey,
        string operationType,
        string? requestHash = null,
        TimeSpan? expiresIn = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationType);

        var expiresAt = DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromHours(24));

        return new IdempotencyRecord(idempotencyKey, operationType, requestHash, expiresAt);
    }

    public void SetResponse(string? responseData, int statusCode)
    {
        ResponseData = responseData;
        StatusCode = statusCode;
    }

    public bool IsExpired() => DateTime.UtcNow >= ExpiresAt;
}
