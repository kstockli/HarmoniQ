namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Kandidat zur Übernahme (Spec §5). Der strukturierte Vorschlag steckt flexibel in
/// <see cref="DatenJson"/> und wird beim Übernehmen auf die bestehenden Find-or-create-Services
/// gemappt (Konzert → KonzertErfassungService, Leitung → BandMitgliedschaft). Nichts wird
/// automatisch publiziert – die Übernahme erfolgt nur durch Admin-Klick.
/// </summary>
public class CrawlFund : AuditierteEntitaet
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Zugehöriger Crawl-Lauf; <c>null</c> bei Funden ohne Lauf (z. B. Wikipedia-Anreicherung).</summary>
    public Guid? LaufId { get; set; }
    public CrawlLauf? Lauf { get; set; }

    public CrawlFundTyp Typ { get; set; } = CrawlFundTyp.Konzert;

    /// <summary>Provenienz: Quell-URL des Funds.</summary>
    public string QuellUrl { get; set; } = string.Empty;

    /// <summary>Stabiler, quellen-eigener Schlüssel des Fund-Gegenstands (z. B. vivenu-Event-ID).
    /// Ermöglicht Dedup über Läufe hinweg: ist dieser Schlüssel schon als Übernommen/Verworfen
    /// bekannt, erzeugt ein erneuter Lauf keinen neuen Fund. <c>null</c> = kein stabiler Schlüssel.</summary>
    public string? ExternKey { get; set; }

    public DateTime AbgerufenAm { get; set; } = DateTime.UtcNow;

    /// <summary>Strukturierter Vorschlag als JSON (z. B. Konzert + Programmzeilen bzw. Person + Band).</summary>
    public string DatenJson { get; set; } = string.Empty;

    /// <summary>Optionale Konfidenz aus Heuristik/LLM.</summary>
    public Konfidenz? Konfidenz { get; set; }

    /// <summary>Hinweis „existiert evtl. schon als …“ aus dem Dedup-Abgleich.</summary>
    public string? DublettHinweis { get; set; }

    public CrawlFundStatus Status { get; set; } = CrawlFundStatus.Offen;
    public DateTime? EntschiedenAm { get; set; }
}
