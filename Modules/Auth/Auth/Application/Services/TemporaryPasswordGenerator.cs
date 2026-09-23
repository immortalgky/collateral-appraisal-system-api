using System.Security.Cryptography;
using Auth.Domain.Configuration;

namespace Auth.Application.Services;

/// <summary>
/// Builds the one-shot password handed out when an access window is opened on a temporary-access
/// account. The password is shown to the admin once and never stored in readable form, so it is
/// generated rather than chosen — and it has to pass <c>DbPasswordValidator</c> on the way in, which
/// is why it is assembled from the policy's own required character classes instead of sampled blindly.
/// </summary>
public static class TemporaryPasswordGenerator
{
    // Ambiguous glyphs (O/0, l/1/I) are left out: the admin reads this off a screen and types it
    // somewhere else, and a password nobody can transcribe just gets pasted into a chat window.
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%^&*-_=+";

    private const int MinimumLength = 20;

    public static string Generate(PasswordPolicy policy)
    {
        // Always include every class, not only the ones the policy demands: a stricter policy set
        // later must not invalidate passwords this issues, and extra entropy costs nothing here.
        List<string> required = [Upper, Lower, Digits, Symbols];
        var alphabet = string.Concat(required);

        // Long enough for the policy's length and its distinct-character rule, never below our own
        // floor. The policy clamps RequiredUniqueChars at 128, which is more distinct characters than
        // the alphabet holds — an impossible demand, and one no admin can express through the screen
        // (the validator there stops well short). Cap at the alphabet size so we issue the strongest
        // password that can exist rather than looping forever on one that cannot.
        var distinctWanted = Math.Min(policy.RequiredUniqueChars, alphabet.Length);
        var length = Math.Max(MinimumLength, Math.Max(policy.RequiredLength, distinctWanted));

        // Take one character from each class FIRST, drawing without replacement throughout. Both
        // rules have to hold at once, and satisfying them in sequence does not work: a draw that only
        // chases distinctness can come back with no digit (a ~5% chance at 20 distinct characters),
        // and topping up afterwards would need a character the distinct budget has no room for.
        // Either way DbPasswordValidator rejects the password and the request fails outright.
        var pool = alphabet.ToList();
        var chars = new List<char>(length);

        char Take(List<char> from)
        {
            var index = RandomNumberGenerator.GetInt32(from.Count);
            var picked = from[index];
            pool.Remove(picked);
            return picked;
        }

        foreach (var set in required)
            chars.Add(Take([.. set.Where(pool.Contains)]));

        while (chars.Count < distinctWanted)
            chars.Add(Take(pool));

        // Past the distinct requirement, repeats are fine — only the total length is left to satisfy.
        while (chars.Count < length)
            chars.Add(alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]);

        // Shuffle so the leading characters aren't always one-per-class in a fixed order.
        RandomNumberGenerator.Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(chars));
        return new string([.. chars]);
    }
}
