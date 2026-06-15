using System.Text.RegularExpressions;

namespace HarmoniQ.Web.Services;

/// <summary>Extrahiert die 11-stellige YouTube-Video-ID aus einer URL oder Eingabe.</summary>
public static partial class YouTubeId
{
    [GeneratedRegex(@"(?:youtu\.be/|youtube\.com/(?:watch\?v=|embed/|shorts/|v/))([A-Za-z0-9_-]{11})")]
    private static partial Regex UrlPattern();

    [GeneratedRegex(@"^[A-Za-z0-9_-]{11}$")]
    private static partial Regex IdPattern();

    /// <summary>
    /// Liefert die Video-ID aus einer YouTube-URL (verschiedene Formate) oder gibt die
    /// Eingabe zurück, falls sie bereits eine gültige 11-stellige ID ist. Sonst leer.
    /// </summary>
    public static string Extrahiere(string? eingabe)
    {
        if (string.IsNullOrWhiteSpace(eingabe)) return "";
        eingabe = eingabe.Trim();

        var match = UrlPattern().Match(eingabe);
        if (match.Success) return match.Groups[1].Value;

        return IdPattern().IsMatch(eingabe) ? eingabe : "";
    }
}
