using System.Security.Cryptography;

namespace Integration.Application.Services;

/// <summary>
/// Produces a deterministic Guid from a seed + secondary id using SHA-1 (RFC 4122 v5 style).
/// Used to give each fan-out webhook a stable eventId across MassTransit retries.
/// </summary>
internal static class DeterministicGuid
{
    internal static Guid Create(Guid seed, Guid secondary)
    {
        Span<byte> input = stackalloc byte[32];
        seed.TryWriteBytes(input[..16]);
        secondary.TryWriteBytes(input[16..]);

        Span<byte> hash = stackalloc byte[20];
        SHA1.HashData(input, hash);

        // Force version=5 and variant=RFC4122 into the byte array
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        return new Guid(hash[..16]);
    }
}
