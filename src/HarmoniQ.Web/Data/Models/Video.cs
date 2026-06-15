namespace HarmoniQ.Web.Data.Models;

public class Video
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StueckId { get; set; }
    public Guid? BandId { get; set; }
    public string YouTubeVideoId { get; set; } = string.Empty;
    public string Titel { get; set; } = string.Empty;
    public DateOnly? AufnahmeDatum { get; set; }
    /// <summary>Optionaler Aufnahme-Ort (z. B. „KKL Luzern“).</summary>
    public string? Ort { get; set; }
    /// <summary>Optionaler Anlass (z. B. „Jahreskonzert 2024“).</summary>
    public string? Anlass { get; set; }
    public VideoStatus Status { get; set; } = VideoStatus.Ausstehend;
    public string? VorgeschlagenVonId { get; set; }
    /// <summary>Zeitpunkt der Erfassung (für „zuletzt hinzugefügt“).</summary>
    public DateTime ErstelltAm { get; set; } = DateTime.UtcNow;

    public Stueck Stueck { get; set; } = null!;
    public Band? Band { get; set; }
    public ApplicationUser? VorgeschlagenVon { get; set; }
    public ICollection<Bewertung> Bewertungen { get; set; } = [];
    public ICollection<VideoMitwirkung> Mitwirkungen { get; set; } = [];
}
