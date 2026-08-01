using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>Einmaliges, <b>non-destruktives</b> Befüllen von Instrument-<see cref="Instrument.Familie"/>
/// und <see cref="Instrument.WikipediaUrl"/>. Nur leere Felder werden gesetzt (Admin-Overrides bleiben).
/// Kuratierte Zuordnung für die gängigen Blasmusik-Instrumente + Keyword-Fallback für Unbekanntes.</summary>
public static class InstrumentSeed
{
    // Name (lowercase) → (Familie, Wikipedia-Titel). Deckt die real vorhandenen Instrumente ab.
    private static readonly Dictionary<string, (InstrumentFamilie F, string Wiki)> Kuratiert = new()
    {
        ["1. posaune"] = (InstrumentFamilie.Blechblaeser, "Posaune"),
        ["2. bariton"] = (InstrumentFamilie.Blechblaeser, "Bariton (Blechblasinstrument)"),
        ["altsaxophon"] = (InstrumentFamilie.Holzblaeser, "Altsaxophon"),
        ["b-bass"] = (InstrumentFamilie.Blechblaeser, "Tuba"),
        ["baritonsaxophon"] = (InstrumentFamilie.Holzblaeser, "Baritonsaxophon"),
        ["bass-klarinette"] = (InstrumentFamilie.Holzblaeser, "Bassklarinette"),
        ["bassklarinette"] = (InstrumentFamilie.Holzblaeser, "Bassklarinette"),
        ["bass-posaune"] = (InstrumentFamilie.Blechblaeser, "Bassposaune"),
        ["englischhorn"] = (InstrumentFamilie.Holzblaeser, "Englischhorn"),
        ["es-bass"] = (InstrumentFamilie.Blechblaeser, "Tuba"),
        ["es-klarinette"] = (InstrumentFamilie.Holzblaeser, "Es-Klarinette"),
        ["euphonium"] = (InstrumentFamilie.Blechblaeser, "Euphonium"),
        ["fagott"] = (InstrumentFamilie.Holzblaeser, "Fagott"),
        ["flöte"] = (InstrumentFamilie.Holzblaeser, "Querflöte"),
        ["harfe"] = (InstrumentFamilie.Saiten, "Harfe"),
        ["klarinette"] = (InstrumentFamilie.Holzblaeser, "Klarinette"),
        ["klavier"] = (InstrumentFamilie.Tasten, "Klavier"),
        ["kontra-fagott"] = (InstrumentFamilie.Holzblaeser, "Kontrafagott"),
        ["kontrabass"] = (InstrumentFamilie.Saiten, "Kontrabass"),
        ["kontrabass-klarinette"] = (InstrumentFamilie.Holzblaeser, "Kontrabassklarinette"),
        ["oboe"] = (InstrumentFamilie.Holzblaeser, "Oboe"),
        ["perkussion"] = (InstrumentFamilie.Schlagwerk, "Perkussion (Musik)"),
        ["piano"] = (InstrumentFamilie.Tasten, "Klavier"),
        ["piccolo"] = (InstrumentFamilie.Holzblaeser, "Pikkoloflöte"),
        ["posaune"] = (InstrumentFamilie.Blechblaeser, "Posaune"),
        ["querflöte/piccolo"] = (InstrumentFamilie.Holzblaeser, "Querflöte"),
        ["saxophon"] = (InstrumentFamilie.Holzblaeser, "Saxophon"),
        ["schlagzeug"] = (InstrumentFamilie.Schlagwerk, "Schlagzeug"),
        ["tenorsaxophon"] = (InstrumentFamilie.Holzblaeser, "Tenorsaxophon"),
        ["trompete"] = (InstrumentFamilie.Blechblaeser, "Trompete"),
        ["trompete/kornett"] = (InstrumentFamilie.Blechblaeser, "Trompete"),
        ["tuba"] = (InstrumentFamilie.Blechblaeser, "Tuba"),
        ["waldhorn"] = (InstrumentFamilie.Blechblaeser, "Horn (Blechblasinstrument)"),
    };

    /// <summary>Setzt fehlende Familie/Wikipedia für alle Instrumente. Liefert die Anzahl geänderter Zeilen.</summary>
    public static async Task<int> BefuellenAsync(ApplicationDbContext db)
    {
        var alle = await db.Instrumente.ToListAsync();
        var geaendert = 0;
        foreach (var i in alle)
        {
            var (fam, wikiTitel) = Bestimme(i.Name);
            var vorher = (i.Familie, i.WikipediaUrl);
            i.Familie ??= fam;
            if (string.IsNullOrWhiteSpace(i.WikipediaUrl) && wikiTitel != null)
                i.WikipediaUrl = WikiUrl(wikiTitel);
            if (vorher != (i.Familie, i.WikipediaUrl)) geaendert++;
        }
        if (geaendert > 0) await db.SaveChangesAsync();
        return geaendert;
    }

    public static string WikiUrl(string titel) =>
        "https://de.wikipedia.org/wiki/" + Uri.EscapeDataString(titel.Replace(' ', '_'));

    /// <summary>Familie + Wikipedia-Titel zu einem Namen: kuratiert, sonst Keyword-Heuristik.</summary>
    public static (InstrumentFamilie, string?) Bestimme(string name)
    {
        var key = name.Trim().ToLowerInvariant();
        if (Kuratiert.TryGetValue(key, out var t)) return (t.F, t.Wiki);

        // Keyword-Fallback (Reihenfolge wichtig: „kontrabass" vor „bass").
        bool Hat(params string[] w) => w.Any(x => key.Contains(x));
        var fam =
            Hat("kontrabass", "geige", "violin", "cello", "harfe", "gitarre", "zither") ? InstrumentFamilie.Saiten :
            Hat("klavier", "piano", "keyboard", "orgel", "akkordeon", "cembalo") ? InstrumentFamilie.Tasten :
            Hat("schlag", "perkuss", "pauke", "drum", "trommel", "marimba", "xylo", "vibra", "becken") ? InstrumentFamilie.Schlagwerk :
            Hat("klarinett", "flöt", "floet", "oboe", "fagott", "saxo", "sax", "piccolo", "pikkolo", "blockflöt") ? InstrumentFamilie.Holzblaeser :
            Hat("trompete", "horn", "posaune", "tuba", "kornett", "bariton", "euphonium", "flügelhorn", "fluegelhorn", "bass", "cornet") ? InstrumentFamilie.Blechblaeser :
            InstrumentFamilie.Sonstige;
        // Wikipedia nur vorschlagen, wenn der Name „sauber" wirkt (kein „/", keine Nummer).
        string? wiki = (key.Contains('/') || char.IsDigit(key[0])) ? null : name.Trim();
        return (fam, wiki);
    }
}
