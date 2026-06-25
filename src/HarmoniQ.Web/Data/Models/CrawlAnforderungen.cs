namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Optionale Anforderungen an einen Crawl-Lauf (Bitset, je <see cref="CrawlQuelle"/> gesetzt).
/// Erweiterbar: neue Anforderungen als weiteres Bit ergänzen.
/// </summary>
[Flags]
public enum CrawlAnforderungen
{
    Keine = 0,

    /// <summary>Konzerte nur als Fund vorschlagen, wenn sie mindestens ein Stück (Programmzeile) haben.</summary>
    KonzertBrauchtStueck = 1,

    /// <summary>(geplant) Vorstandsmitglieder mit erfassen (als Band-Funktion, öffentlich).</summary>
    VorstandCrawlen = 2,

    /// <summary>(geplant) Musikkommission (Muko) mit erfassen.</summary>
    MukoCrawlen = 4
}
