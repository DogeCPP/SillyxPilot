using System.Text;
using System.Text.RegularExpressions;

namespace SillyxPilot.Plugin.Alerting;

public record UsernameCheck(bool Ok, string? Value, string? Reason);

public static class BadWords
{
    private static readonly string[] Hate =
    {
        "nigg", "nigr", "niga", "chink", "spick", "kike", "faggot", "fag",
        "retard", "tranny", "coon", "wetback", "gook", "paki", "beaner",
        "raghead", "sandnigger", "negro", "jap", "kraut", "nazi", "hitler", "kkk",
    };

    private static readonly string[] Profanity =
    {
        "fuck", "shit", "cunt", "bitch", "bastard", "dick", "cock", "pussy",
        "asshole", "whore", "slut", "wank", "twat", "jizz", "dildo", "boner",
        "penis", "vagina", "anus", "rape", "pedo", "porn",
    };

    private static readonly Regex ValidChars = new(@"^[\w \-.]+$", RegexOptions.Compiled);
    private static readonly Regex NonLetterSpace = new(@"[^a-z ]", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

    private static char Leet(char c) => c switch
    {
        '0' => 'o', '1' => 'i', '3' => 'e', '4' => 'a', '5' => 's',
        '7' => 't', '8' => 'b', '@' => 'a', '$' => 's', '!' => 'i',
        _ => c,
    };

    private static (string spaced, string collapsed) Normalise(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var c in input.ToLowerInvariant()) sb.Append(Leet(c));
        var spaced = NonLetterSpace.Replace(sb.ToString(), "");
        var collapsed = WhitespaceRun.Replace(spaced, "");
        return (spaced, collapsed);
    }

    public static UsernameCheck Check(string? name)
    {
        var raw = (name ?? "").Trim();
        if (raw.Length < 2) return new UsernameCheck(false, null, "Username must be at least 2 characters.");
        if (raw.Length > 24) return new UsernameCheck(false, null, "Username must be 24 characters or fewer.");
        if (!ValidChars.IsMatch(raw)) return new UsernameCheck(false, null, "Use only letters, numbers, spaces, - . _");

        var (spaced, collapsed) = Normalise(raw);
        foreach (var term in Hate)
        {
            if (collapsed.Contains(term) || spaced.Contains(term))
                return new UsernameCheck(false, null, "That username contains language we do not allow. Please pick another.");
        }
        foreach (var term in Profanity)
        {
            if (collapsed.Contains(term))
                return new UsernameCheck(false, null, "Please keep usernames clean, no profanity.");
        }
        return new UsernameCheck(true, raw, null);
    }
}
