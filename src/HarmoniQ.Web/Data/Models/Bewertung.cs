namespace HarmoniQ.Web.Data.Models;

public class Bewertung
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VideoId { get; set; }
    public string? BenutzerId { get; set; }
    public string? AnonymerCookieId { get; set; }
    public int GesamtEindruck { get; set; }
    public int Praezision { get; set; }
    public int Musikalitaet { get; set; }
    public int AkustischeQualitaet { get; set; }
    public int VideoQualitaet { get; set; }
    public string? Kommentar { get; set; }
    public DateTime ErstelltAm { get; set; } = DateTime.UtcNow;

    public Video Video { get; set; } = null!;
    public ApplicationUser? Benutzer { get; set; }
}
