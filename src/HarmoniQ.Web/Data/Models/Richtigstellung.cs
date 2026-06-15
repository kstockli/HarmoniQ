namespace HarmoniQ.Web.Data.Models;

/// <summary>Freitext-Hinweis/Korrektur von eingeloggten Usern auf ein Objekt (Video/Stück/Person/Band).</summary>
public class Richtigstellung
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public RichtigstellungTyp BetrifftTyp { get; set; }
    public Guid BetrifftId { get; set; }
    public string Text { get; set; } = string.Empty;

    public string? EingereichtVonId { get; set; }
    public ApplicationUser? EingereichtVon { get; set; }
    public DateTime ErstelltAm { get; set; } = DateTime.UtcNow;

    public RichtigstellungStatus Status { get; set; } = RichtigstellungStatus.Offen;
    public string? Antwort { get; set; }
    public DateTime? AntwortAm { get; set; }
}
