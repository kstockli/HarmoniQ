using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>Bildet je <see cref="VideoPlattform"/> aus der externen ID die Einbett-, Thumbnail- und
/// Verweis-URL. Zentral, damit Komponenten nicht plattform-spezifische URLs inline bauen.</summary>
public static class VideoEinbettung
{
    /// <summary>Iframe-Quelle für den Player (null, wenn keine ID).</summary>
    public static string? Embed(VideoPlattform plattform, string? externId) =>
        string.IsNullOrWhiteSpace(externId) ? null : plattform switch
        {
            VideoPlattform.YouTube => $"https://www.youtube.com/embed/{externId}",
            VideoPlattform.InfomaniakVod => $"https://player.vod2.infomaniak.com/embed/{externId}",
            VideoPlattform.Vimeo => $"https://player.vimeo.com/video/{externId}",
            VideoPlattform.Datei => externId,   // direkte Datei-URL → als <video src> genutzt
            // SRG-Play: ExternId = volle URN (urn:rtr:video:…). Offizieller Embed-Player.
            VideoPlattform.SrgPlay => $"https://www.rtr.ch/play/embed?urn={Uri.EscapeDataString(externId)}",
            _ => null
        };

    /// <summary>True, wenn die Quelle eine direkte Datei-URL ist (per HTML5-&lt;video&gt; statt iframe).</summary>
    public static bool IstDatei(VideoPlattform plattform) => plattform == VideoPlattform.Datei;

    /// <summary>Vorschaubild. Ein explizit gespeichertes <paramref name="bildUrl"/> (z. B. SRG/RTR-Play, wo das
    /// Thumbnail NICHT aus der ID ableitbar ist) hat Vorrang. Sonst: YouTube/Infomaniak aus der ID berechnet,
    /// alle übrigen ein neutraler Platzhalter (damit Listen-Markup unverändert mit einem &lt;img&gt; funktioniert).</summary>
    public static string Thumbnail(VideoPlattform plattform, string? externId, string groesse = "hqdefault", string? bildUrl = null) =>
        !string.IsNullOrWhiteSpace(bildUrl) ? bildUrl!
        : string.IsNullOrWhiteSpace(externId) ? Platzhalter : plattform switch
        {
            VideoPlattform.YouTube => $"https://i.ytimg.com/vi/{externId}/{groesse}.jpg",
            // Infomaniak VOD: das Poster/Standbild ist aus der Embed-ID ableitbar (= <video poster>).
            VideoPlattform.InfomaniakVod => $"https://api.infomaniak.com/2/vod/res/shares/{externId}.preload.jpeg",
            _ => Platzhalter
        };

    /// <summary>Direktlink zum Ansehen (YouTube → youtu.be; sonst die Embed-/Player-URL).</summary>
    public static string? ExternLink(VideoPlattform plattform, string? externId) =>
        string.IsNullOrWhiteSpace(externId) ? null : plattform switch
        {
            VideoPlattform.YouTube => $"https://youtu.be/{externId}",
            _ => Embed(plattform, externId)
        };

    /// <summary>Neutrales Video-Platzhalterbild (inline SVG data-URI, dunkel mit Play-Dreieck).</summary>
    public const string Platzhalter =
        "data:image/svg+xml;utf8," +
        "%3Csvg xmlns='http://www.w3.org/2000/svg' width='320' height='180'%3E" +
        "%3Crect width='320' height='180' fill='%231a0030'/%3E" +
        "%3Cpolygon points='135,65 135,115 180,90' fill='%239B59B6'/%3E%3C/svg%3E";
}
