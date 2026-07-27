using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Erkennt aus einer Nutzereingabe die Video-Quelle: YouTube-Link/-ID ODER eine direkte Datei-URL
/// (mp4/webm/… auf eigenem Webspace). Liefert Plattform + ExternId; null wenn nichts erkannt wurde.
/// </summary>
public static class VideoQuelle
{
    public static (VideoPlattform Plattform, string ExternId)? Parse(string? eingabe)
    {
        if (string.IsNullOrWhiteSpace(eingabe)) return null;
        var s = eingabe.Trim();

        // 1) YouTube (URL-Varianten oder nackte 11-stellige ID)
        var yt = YouTubeId.Extrahiere(s);
        if (!string.IsNullOrEmpty(yt)) return (VideoPlattform.YouTube, yt);

        // 2) Direkte Datei-URL (http/https) → per <video> abgespielt, volle URL als ExternId
        if (Uri.TryCreate(s, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            return (VideoPlattform.Datei, s);

        return null;
    }

    /// <summary>Leitet einen Anzeige-Titel aus einer Datei-URL ab (Dateiname ohne Endung),
    /// z. B. .../BOFMN_Kerkrade_2026_Aufgabenstueck.mp4 → "BOFMN Kerkrade 2026 Aufgabenstueck".</summary>
    public static string? TitelAusDateiUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        var name = Uri.UnescapeDataString(uri.Segments.LastOrDefault()?.Trim('/') ?? "");
        if (string.IsNullOrWhiteSpace(name)) return null;
        var punkt = name.LastIndexOf('.');
        if (punkt > 0) name = name[..punkt];
        name = name.Replace('_', ' ').Replace('-', ' ').Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
