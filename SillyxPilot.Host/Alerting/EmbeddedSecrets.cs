using System;
using System.IO;
using System.Text.Json.Nodes;

namespace SillyxPilot.Plugin.Alerting;

public record SillyMailConfig(string Host, int Port, bool Secure, string User, string Pass, string From);
public record Secrets(SillyMailConfig SillyMail, string DiscordBotToken);

public static class EmbeddedSecrets
{
    private const string Key = "sxp!7f3a9d2b-keychain";

    private const string EncUser = "";
    private const string EncPass = "";
    private const string EncFrom = "";
    private const string EncBotToken = "";

    private static string Decode(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return "";
        var raw = Convert.FromBase64String(base64);
        var chars = new char[raw.Length];
        for (int i = 0; i < raw.Length; i++) chars[i] = (char)(raw[i] ^ Key[i % Key.Length]);
        return new string(chars);
    }

    public static string EncodeForBaking(string plain)
    {
        var bytes = new byte[plain.Length];
        for (int i = 0; i < plain.Length; i++) bytes[i] = (byte)(plain[i] ^ Key[i % Key.Length]);
        return Convert.ToBase64String(bytes);
    }

    private static Secrets Builtin()
    {
        var user = Decode(EncUser);
        var from = string.IsNullOrEmpty(EncFrom) && !string.IsNullOrEmpty(user)
            ? $"SillyxPilot Alerts <{user}>" : Decode(EncFrom);
        return new Secrets(new SillyMailConfig("smtp.gmail.com", 465, true, user, Decode(EncPass), from), Decode(EncBotToken));
    }

    private static string OverridePath => Path.Combine(AppPaths.SillyDataDir(), "secrets.json");
    private static Secrets? _cache;

    public static Secrets Get()
    {
        if (_cache != null) return _cache;
        var builtin = Builtin();
        if (File.Exists(OverridePath) && Runtime.TryReadText(OverridePath, out var text))
        {
            try
            {
                var root = JsonNode.Parse(text)?.AsObject();
                var mail = builtin.SillyMail;
                if (root?["sillyMail"] is JsonObject m)
                {
                    string GetOr(string name, string fb) => m[name]?.GetValue<string>() ?? fb;
                    mail = new SillyMailConfig(
                        GetOr("host", mail.Host),
                        m["port"]?.GetValue<int>() ?? mail.Port,
                        m["secure"]?.GetValue<bool>() ?? mail.Secure,
                        GetOr("user", mail.User),
                        GetOr("pass", mail.Pass),
                        GetOr("from", mail.From));
                }
                var botToken = root?["discordBotToken"]?.GetValue<string>() ?? builtin.DiscordBotToken;
                _cache = new Secrets(mail, botToken);
                return _cache;
            }
            catch {  }
        }
        _cache = builtin;
        return _cache;
    }

    public static bool HasMailSender() => !string.IsNullOrEmpty(Get().SillyMail.User) && !string.IsNullOrEmpty(Get().SillyMail.Pass);
    public static bool HasBotToken() => Get().DiscordBotToken.Length > 10;
}
