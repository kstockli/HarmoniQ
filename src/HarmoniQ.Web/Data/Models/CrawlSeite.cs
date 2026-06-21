namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Besuchte Seite je Quelle – für Dedup/Politeness über Läufe hinweg (Spec §5, optional).
/// Verhindert Doppel-Abrufe und erlaubt „nur bei geändertem Inhalt neu extrahieren“.
/// </summary>
public class CrawlSeite
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid QuelleId { get; set; }
    public CrawlQuelle Quelle { get; set; } = null!;

    public string Url { get; set; } = string.Empty;

    /// <summary>Hash des extrahierten Inhalts (Änderungserkennung).</summary>
    public string? InhaltsHash { get; set; }

    public DateTime AbgerufenAm { get; set; } = DateTime.UtcNow;

    /// <summary>Ob die Seite vom Seiten-Filter als relevant eingestuft wurde.</summary>
    public bool Relevant { get; set; }
}
