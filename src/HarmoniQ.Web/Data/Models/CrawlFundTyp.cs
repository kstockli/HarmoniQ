namespace HarmoniQ.Web.Data.Models;

/// <summary>Art eines <see cref="CrawlFund"/> – bestimmt den Übernahme-Pfad bei der Review.</summary>
public enum CrawlFundTyp
{
    /// <summary>Konzert inkl. Programm (Stück + Komponist:in + Band) → KonzertErfassungService.</summary>
    Konzert = 0,

    /// <summary>Leitung/Dirigent:in einer Band → BandMitgliedschaft (Funktion „Dirigent“).</summary>
    Leitung = 1,

    /// <summary>Einzelnes Stück (z. B. Repertoire/Werkliste, ohne Konzertbezug) → Stück + StückBeitrag.</summary>
    Stueck = 2,

    /// <summary>Komponist:in zum Anlegen/Anreichern (z. B. Wikipedia: Bio/Bild/Geburtsjahr) → Person.</summary>
    Komponist = 3,

    /// <summary>Vereins-/Band-Stammdaten (Name, Aliase, Kategorie, Stärkeklasse, Gründungsjahr,
    /// Geschichte, Links) – meist von der eigenen Vereinsseite → Band (find-or-create, leere Felder füllen).</summary>
    Band = 4,

    /// <summary>Entdeckte Vereins-Webseite (aus der Link-Ernte einer Event-Seite) mit Mini-Vorschau.
    /// Übernahme legt eine inaktive BandDomain-Quelle (Vorschlag) an.</summary>
    Webseite = 5,

    /// <summary>YouTube-Video einer Band (aus dem Kanal-Crawl §4.5): Stück/Komponist:in/Ort/Anlass als
    /// Vorschlag (per LLM aus Titel+Beschreibung). Übernahme → <see cref="Video"/> (find-or-create Stück).</summary>
    Video = 6,

    /// <summary>Stück-Beschreibung-Anreicherung (§4.9): kurze eigene Sachnotiz (+ Jahr) zu einem bestehenden
    /// Stück. Übernahme setzt <c>Stueck.Beschreibung</c>/<c>Jahr</c> (nur leere Felder).</summary>
    StueckBeschreibung = 7,

    /// <summary>Dubletten-Vorschlag (§4.10): mutmassliches Duplikat-Paar (Stück oder Person). Übernahme führt
    /// die Quelle ins Ziel zusammen (Merge, Quell-Name bleibt als Alias). Bewusst nur mit Review.</summary>
    Dublette = 8,

    /// <summary>Sonstiger Fund (z. B. Webseiten-Vorschlag, noch nicht zugeordnet).</summary>
    Sonstiges = 99
}
