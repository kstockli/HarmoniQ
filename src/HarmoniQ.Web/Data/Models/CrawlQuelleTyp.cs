namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Art einer <see cref="CrawlQuelle"/> – bestimmt das Vorgehen der Pipeline (Spec §4.1).
/// </summary>
public enum CrawlQuelleTyp
{
    /// <summary>Vereins-Webseite: auf der Domain crawlen, Heuristik/LLM, Leitung &amp; Konzerte.</summary>
    BandDomain = 0,

    /// <summary>Einzelnes Dokument/PDF (Rangliste, Spielplan): Text extrahieren → strukturieren.</summary>
    Dokument = 1,

    /// <summary>Event-Seite (Festival-Spielplan, oft JS/SPA): rendern → Programm extrahieren.</summary>
    Event = 2
}
