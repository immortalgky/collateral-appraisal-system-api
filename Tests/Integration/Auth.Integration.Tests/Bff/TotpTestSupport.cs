using System.Security.Cryptography;
using Auth.Application.Services;
using Auth.Domain.Identity;
using Auth.Infrastructure.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Auth.Infrastructure;

namespace Integration.Auth.Integration.Tests.Bff;

/// <summary>RFC 6238 (SHA-1, 30 s, 6 digits) from the stored authenticator key, and the two-factor set-up the tests start from.</summary>
public static class TotpTestSupport
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>The code an authenticator app shows <paramref name="stepOffset"/> 30-second steps from now.</summary>
    public static string Code(string base32Key, int stepOffset = 0)
    {
        var key = Base32Decode(base32Key);
        var step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30 + stepOffset;
        var counter = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian) Array.Reverse(counter);

        var hash = HMACSHA1.HashData(key, counter);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    /// <summary>A 6-digit code that is not valid now (Identity accepts a window of steps around the current one).</summary>
    public static string WrongCode(string base32Key)
    {
        var valid = Enumerable.Range(-3, 7).Select(o => Code(base32Key, o)).ToHashSet();
        return Enumerable.Range(0, 1_000_000).Select(i => i.ToString("D6")).First(c => !valid.Contains(c));
    }

    private static byte[] Base32Decode(string value)
    {
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in value.TrimEnd('=').ToUpperInvariant())
        {
            buffer = (buffer << 5) | Base32Alphabet.IndexOf(c);
            bits += 5;
            if (bits < 8) continue;
            bytes.Add((byte)(buffer >> (bits - 8)));
            bits -= 8;
        }

        return bytes.ToArray();
    }

    public static async Task<string?> KeyOfAsync(IServiceProvider services, Guid userId)
    {
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        return await users.GetAuthenticatorKeyAsync(user!);
    }

    /// <summary>Turns two-factor on through the real service, as the profile page would. Returns the recovery codes.</summary>
    public static async Task<IReadOnlyList<string>> EnableAsync(IServiceProvider services, TestUser testUser)
    {
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var twoFactor = scope.ServiceProvider.GetRequiredService<TwoFactorService>();
        var user = (await users.FindByIdAsync(testUser.Id.ToString()))!;

        await twoFactor.BeginSetupAsync(user);
        var key = (await users.GetAuthenticatorKeyAsync(user))!;
        return await twoFactor.EnableAsync(user, Code(key));
    }

    /// <summary>Runs a SET clause on the one password-policy row and drops the cached copy. Returns an undo.</summary>
    public static async Task<IAsyncDisposable> SetPolicyAsync(IServiceProvider services, string set, string undo)
    {
        await ExecuteAsync(services, $"UPDATE auth.PasswordPolicy SET {set}");
        return new Undo(() => ExecuteAsync(services, $"UPDATE auth.PasswordPolicy SET {undo}"));
    }

    private static async Task ExecuteAsync(IServiceProvider services, string sql)
    {
        await using var scope = services.CreateAsyncScope();
        // Test-controlled literals only.
#pragma warning disable EF1002
        await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.ExecuteSqlRawAsync(sql);
#pragma warning restore EF1002
        scope.ServiceProvider.GetRequiredService<IPasswordPolicyProvider>().Invalidate();
    }

    private sealed class Undo(Func<Task> undo) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => new(undo());
    }
}
