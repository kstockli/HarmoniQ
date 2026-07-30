using System.ComponentModel.DataAnnotations.Schema;
using HarmoniQ.Web.Services;

namespace HarmoniQ.Web.Data.Models;

public class Video : AuditierteEntitaet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StueckId { get; set; }
    public Guid? BandId { get; set; }
    /// <summary>Optionaler Verweis auf das Konzert/den Auftritt, an dem die Aufnahme entstand.</summary>
    public Guid? KonzertId { get; set; }
    /// <summary>Video-Quelle (Default YouTube). Bestimmt die Einbettung (siehe <see cref="EmbedUrl"/>).</summary>
    public VideoPlattform Plattform { get; set; } = VideoPlattform.YouTube;
    /// <summary>Plattform-spezifische ID (YouTube: 11-stellig; InfomaniakVod: Embed-ID). Früher YouTubeVideoId.</summary>
    public string ExternId { get; set; } = string.Empty;
    public string Titel { get; set; } = string.Empty;
    public DateOnly? AufnahmeDatum { get; set; }
    /// <summary>Optionaler Aufnahme-Ort (z. B. „KKL Luzern“).</summary>
    public string? Ort { get; set; }
    /// <summary>Optionaler Anlass (z. B. „Jahreskonzert 2024“).</summary>
    public string? Anlass { get; set; }
    public VideoStatus Status { get; set; } = VideoStatus.Ausstehend;
    /// <summary>Optionale explizite Vorschaubild-URL. Nötig für Plattformen, deren Thumbnail sich NICHT aus
    /// der <see cref="ExternId"/> ableiten lässt (z. B. SRG/RTR-Play – Bild liegt auf einem CDN mit eigener
    /// ID). Wird bevorzugt vor der plattform-berechneten <see cref="ThumbnailUrl"/> verwendet.</summary>
    public string? BildUrl { get; set; }
    public string? VorgeschlagenVonId { get; set; }
    /// <summary>Zeitpunkt der Erfassung (für „zuletzt hinzugefügt“).</summary>
    public DateTime ErstelltAm { get; set; } = DateTime.UtcNow;

    // ─── Berechnete Einbettung je Plattform (nicht in der DB) ───
    [NotMapped] public string? EmbedUrl => VideoEinbettung.Embed(Plattform, ExternId);
    [NotMapped] public string ThumbnailUrl =>
        !string.IsNullOrWhiteSpace(BildUrl) ? BildUrl : VideoEinbettung.Thumbnail(Plattform, ExternId);

    // ─── Effektiver Ort/Anlass: das verknüpfte Konzert ist die Quelle der Wahrheit ───
    // (Konzert.Lokal.Name → Konzert.Ort → eigener Freitext). Nur wenn kein Konzert verknüpft ist,
    // gilt der Freitext des Videos. Voraussetzung: Konzert (+ Lokal) sind geladen.
    /// <summary>Anzuzeigender Aufnahme-Ort (bevorzugt aus dem verknüpften Konzert/Lokal).</summary>
    [NotMapped] public string? EffektiverOrt => Konzert?.Lokal?.Name ?? Konzert?.Ort ?? Ort;
    /// <summary>Anzuzeigender Anlass (bevorzugt aus dem verknüpften Konzert).</summary>
    [NotMapped] public string? EffektiverAnlass => Konzert?.Name ?? Anlass;

    public Stueck Stueck { get; set; } = null!;
    public Band? Band { get; set; }
    public Konzert? Konzert { get; set; }
    public ApplicationUser? VorgeschlagenVon { get; set; }
    public ICollection<Bewertung> Bewertungen { get; set; } = [];
    public ICollection<VideoMitwirkung> Mitwirkungen { get; set; } = [];
}
