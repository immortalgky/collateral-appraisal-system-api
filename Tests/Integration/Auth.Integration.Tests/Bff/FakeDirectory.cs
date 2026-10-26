using System.Collections.Concurrent;

namespace Integration.Auth.Integration.Tests.Bff;

/// <summary>What the fake Active Directory says about an account. Users not listed are normal accounts that never expire.</summary>
public static class FakeDirectory
{
    private static readonly ConcurrentDictionary<string, Entry> Accounts = new();
    private static readonly ConcurrentDictionary<string, bool> Unavailable = new();

    /// <summary>What AD reports for an account: <c>userAccountControl</c>, <c>accountExpires</c>, and the computed control bits.</summary>
    public sealed record Entry(int UserAccountControl, long AccountExpires, int Computed);

    private static readonly Entry Normal = new(512, 0, 0);

    public static void Set(string userName, int userAccountControl = 512, long accountExpires = 0, int computed = 0) =>
        Accounts[userName] = new Entry(userAccountControl, accountExpires, computed);

    /// <summary>The directory cannot be reached for this account (the lookup fails).</summary>
    public static void MakeUnavailable(string userName) => Unavailable[userName] = true;

    public static Entry? Lookup(string userName) =>
        Unavailable.ContainsKey(userName) ? null : Accounts.GetValueOrDefault(userName, Normal);
}
