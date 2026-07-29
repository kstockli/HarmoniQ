namespace HarmoniQ.Web.Data.Models;

/// <summary>Video-Quelle/Plattform. Bestimmt, wie aus <see cref="Video.ExternId"/> die Einbett-/
/// Thumbnail-URL gebildet wird (siehe <c>VideoEinbettung</c>). Default <see cref="YouTube"/>.</summary>
public enum VideoPlattform
{
    YouTube = 0,
    /// <summary>Infomaniak VOD (z. B. SBBW): <c>player.vod2.infomaniak.com/embed/&lt;id&gt;</c>.</summary>
    InfomaniakVod = 1,
    Vimeo = 2,
    Andere = 3,
    /// <summary>Direkte Video-Datei-URL (mp4/webm/mov …) auf eigenem Webspace; wird per
    /// HTML5-&lt;video&gt; abgespielt. In <c>ExternId</c> steht die vollständige URL.</summary>
    Datei = 4,
    /// <summary>SRG/SRF/RTS/RTR „Play" (z. B. EMF-Parademusik): offizieller Embed-Player
    /// <c>rtr.ch/play/embed?urn=…</c>. In <c>ExternId</c> steht die volle URN (<c>urn:rtr:video:&lt;id&gt;</c>).</summary>
    SrgPlay = 5
}
