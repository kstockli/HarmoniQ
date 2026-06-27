namespace HarmoniQ.Web.Data.Models;

/// <summary>Video-Quelle/Plattform. Bestimmt, wie aus <see cref="Video.ExternId"/> die Einbett-/
/// Thumbnail-URL gebildet wird (siehe <c>VideoEinbettung</c>). Default <see cref="YouTube"/>.</summary>
public enum VideoPlattform
{
    YouTube = 0,
    /// <summary>Infomaniak VOD (z. B. SBBW): <c>player.vod2.infomaniak.com/embed/&lt;id&gt;</c>.</summary>
    InfomaniakVod = 1,
    Vimeo = 2,
    Andere = 3
}
