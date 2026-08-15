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
    Event = 2,

    /// <summary>Wettbewerb (SBBW, swissbrass.ch): Spezial-Handler – Jahres-PDF (Rangliste je Kategorie)
    /// + Video-Seiten → je Jahr/Kategorie ein Konzert mit Rangliste &amp; Videos (Spec §4.2).</summary>
    Wettbewerb = 3,

    /// <summary>Veranstalter-Eventseite (z. B. KKL Luzern, vivenu-Ticketing): Spezial-Handler –
    /// Eventliste rendern, Event-Daten aus der vivenu-API, LLM-Stilfilter (§4.3).</summary>
    Veranstalter = 4,

    /// <summary>Aggregat-Quelle „YouTube über alle Bands" (§4.5): fächert über alle Bands mit hinterlegtem
    /// YouTube-Kanal auf, geht deren Uploads durch (Kanal-basiert, nur Videos ≥ 2 Min) und legt neue Treffer
    /// als Video-<see cref="CrawlFund"/> (Stück/Komponist:in/Ort/Anlass per LLM) an. Keine Start-URL/Ziel-Band.</summary>
    BandVideos = 5,

    /// <summary>Aggregat-Quelle „Künftige Konzerte über alle Band-Webseiten" (§4.8, Handler in CrawlRunner):
    /// fächert über alle Bands
    /// mit Webseite auf, sucht auf Startseite + Agenda/Kalender nach <b>künftigen, echten</b> Konzerten
    /// (Typ-Filter: Jahres-/Gala-/Kirchenkonzert etc.; keine Kilbi/Ständchen/Heim-Auftritte) und legt sie als
    /// Konzert-<see cref="CrawlFund"/> an. Keine Start-URL/Ziel-Band.</summary>
    BandKonzertVorschau = 6
}
