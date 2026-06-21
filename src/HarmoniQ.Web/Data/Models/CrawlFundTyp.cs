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

    /// <summary>Sonstiger Fund (z. B. Webseiten-Vorschlag, noch nicht zugeordnet).</summary>
    Sonstiges = 99
}
