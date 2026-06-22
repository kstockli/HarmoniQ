namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Seed/Quelle für den Crawler (Spec §5): eine Band-Domain, ein Dokument/PDF oder eine Event-Seite.
/// Isoliert vom Kernmodell – einziger FK-Berührungspunkt ist die optionale Ziel-<see cref="Band"/>.
/// </summary>
public class CrawlQuelle
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public CrawlQuelleTyp Typ { get; set; } = CrawlQuelleTyp.BandDomain;

    /// <summary>Zielband (bei <see cref="CrawlQuelleTyp.BandDomain"/>; sonst optional).</summary>
    public Guid? BandId { get; set; }
    public Band? Band { get; set; }

    /// <summary>Domain-Start, PDF-/Dokument-Link oder Event-Seite.</summary>
    public string StartUrl { get; set; } = string.Empty;

    /// <summary>Bei <see cref="CrawlQuelleTyp.BandDomain"/>: der Crawler bleibt auf dieser Domain.</summary>
    public string? Domain { get; set; }

    /// <summary>Event/SPA: Seite per Headless-Browser rendern (Phase C2).</summary>
    public bool BrauchtRendering { get; set; }

    /// <summary>Optionaler Freitext-Hinweis, der der LLM-Extraktion zusätzlich mitgegeben wird
    /// (z. B. „Nur die Wettspielvorträge in der Kirche, nicht in der Arche").</summary>
    public string? ExtraktionsHinweis { get; set; }

    /// <summary>Maximale Linktiefe (nur BandDomain).</summary>
    public int MaxTiefe { get; set; } = 2;

    /// <summary>Maximale Seitenzahl je Lauf (nur BandDomain).</summary>
    public int MaxSeiten { get; set; } = 100;

    public bool Aktiv { get; set; } = true;
    public DateTime ErstelltAm { get; set; } = DateTime.UtcNow;
    public DateTime? LetzterLaufAm { get; set; }

    public ICollection<CrawlLauf> Laeufe { get; set; } = [];
}
