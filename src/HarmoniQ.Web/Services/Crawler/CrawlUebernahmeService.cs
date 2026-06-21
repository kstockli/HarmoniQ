using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services.Crawler;

/// <summary>
/// Übernimmt einen <see cref="CrawlFund"/> in die echten Daten (Spec §7) – nur durch Admin-Klick.
/// Nutzt die bestehenden Find-or-create-Bausteine, damit keine Dubletten entstehen:
/// Konzert-Funde → <see cref="KonzertErfassungService"/>; Leitung-Funde → <see cref="BandMitgliedschaft"/>.
/// Die Quell-URL bleibt über den Fund als Provenienz erhalten.
/// </summary>
public static class CrawlUebernahmeService
{
    /// <summary>
    /// Übernimmt den Fund anhand der (ggf. im Review editierten) <paramref name="datenJson"/>.
    /// Wirft <see cref="InvalidOperationException"/>, wenn Pflichtangaben fehlen (z. B. Konzert-Datum).
    /// </summary>
    public static async Task UebernehmenAsync(ApplicationDbContext db, Guid fundId, string datenJson)
    {
        var fund = await db.CrawlFunde.FirstOrDefaultAsync(f => f.Id == fundId)
            ?? throw new InvalidOperationException("Fund nicht gefunden.");
        if (fund.Status != CrawlFundStatus.Offen)
            throw new InvalidOperationException("Fund ist bereits entschieden.");

        switch (fund.Typ)
        {
            case CrawlFundTyp.Konzert:
                await KonzertUebernehmenAsync(db, datenJson);
                break;
            case CrawlFundTyp.Leitung:
                await LeitungUebernehmenAsync(db, fund, datenJson);
                break;
            case CrawlFundTyp.Stueck:
                await StueckUebernehmenAsync(db, fund, datenJson);
                break;
            case CrawlFundTyp.Komponist:
                await KomponistUebernehmenAsync(db, datenJson);
                break;
            default:
                throw new InvalidOperationException(
                    "Funde vom Typ „Sonstiges“ werden manuell bearbeitet, nicht automatisch übernommen.");
        }

        fund.Status = CrawlFundStatus.Uebernommen;
        fund.EntschiedenAm = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public static async Task VerwerfenAsync(ApplicationDbContext db, Guid fundId)
    {
        var fund = await db.CrawlFunde.FirstOrDefaultAsync(f => f.Id == fundId)
            ?? throw new InvalidOperationException("Fund nicht gefunden.");
        if (fund.Status != CrawlFundStatus.Offen) return;
        fund.Status = CrawlFundStatus.Verworfen;
        fund.EntschiedenAm = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private static async Task KonzertUebernehmenAsync(ApplicationDbContext db, string datenJson)
    {
        var d = CrawlDaten.Deserialisiere<KonzertFundDaten>(datenJson)
            ?? throw new InvalidOperationException("Konzert-Daten konnten nicht gelesen werden.");
        if (d.Datum is not { } datum)
            throw new InvalidOperationException("Konzert-Datum fehlt – bitte im Review ergänzen.");

        var programm = (d.Programm ?? [])
            .Where(z => !string.IsNullOrWhiteSpace(z.StueckTitel))
            .Select(z => new KonzertErfassungService.ProgrammEingabe(
                z.StueckTitel, z.KomponistName, z.BandName, z.Reihenfolge))
            .ToList();

        var eingabe = new KonzertErfassungService.Eingabe(
            Datum: datum,
            Name: d.Name,
            Ort: d.Ort,
            Beschreibung: d.Beschreibung,
            BildUrl: null,
            Programm: programm,
            Mitwirkende: []);

        await KonzertErfassungService.ErfasseAsync(db, eingabe);
    }

    private static async Task LeitungUebernehmenAsync(ApplicationDbContext db, CrawlFund fund, string datenJson)
    {
        var d = CrawlDaten.Deserialisiere<LeitungFundDaten>(datenJson)
            ?? throw new InvalidOperationException("Leitung-Daten konnten nicht gelesen werden.");
        var name = d.PersonName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Personenname fehlt.");

        var band = await BandAufloesenAsync(db, fund, d.BandName)
            ?? throw new InvalidOperationException("Keine Band bestimmbar – Bandname fehlt und Quelle hat keine Ziel-Band.");

        // Person find-or-create (+ Rolle Dirigent). Dirigent:in ist öffentlich (Funktionsträger:in).
        var person = await db.Personen.Include(p => p.Rollen).FirstOrDefaultAsync(p => p.Name == name);
        if (person == null)
        {
            person = new Person { Name = name, Sichtbarkeit = Sichtbarkeit.Oeffentlich };
            db.Personen.Add(person);
        }
        if (person.Rollen.All(r => r.Rolle != PersonRolleTyp.Dirigent))
            person.Rollen.Add(new PersonRolle { Rolle = PersonRolleTyp.Dirigent });

        var funktion = string.IsNullOrWhiteSpace(d.Funktion) ? "Dirigent" : d.Funktion.Trim();

        // Keine Dublette: gleiche Person + Band + Funktion (laufende Mitgliedschaft) nicht doppelt.
        var existiert = await db.BandMitgliedschaften.AnyAsync(m =>
            m.BandId == band.Id && m.Person.Name == name && m.Funktion == funktion);
        if (!existiert)
            db.BandMitgliedschaften.Add(new BandMitgliedschaft
            {
                Band = band,
                Person = person,
                Funktion = funktion,
                VonJahr = d.VonJahr,
                BisJahr = d.BisJahr
            });
    }

    private static async Task StueckUebernehmenAsync(ApplicationDbContext db, CrawlFund fund, string datenJson)
    {
        var d = CrawlDaten.Deserialisiere<StueckFundDaten>(datenJson)
            ?? throw new InvalidOperationException("Stück-Daten konnten nicht gelesen werden.");
        var titel = d.Titel?.Trim();
        if (string.IsNullOrWhiteSpace(titel))
            throw new InvalidOperationException("Stück-Titel fehlt.");

        // Find-or-create Stück (Abgleich über Titel, normalisiert).
        var stueck = await db.Stuecke.FirstOrDefaultAsync(s => s.Titel == titel);
        if (stueck == null)
        {
            stueck = new Stueck
            {
                Titel = titel,
                Jahr = d.Jahr,
                Schwierigkeitsgrad = d.Schwierigkeit ?? Schwierigkeitsgrad.Unbekannt,
                Besetzung = Leer(d.Besetzung),
                Beschreibung = Leer(d.Beschreibung),
                OriginalUrl = Leer(fund.QuellUrl)
            };
            db.Stuecke.Add(stueck);
        }

        // Optional Komponist:in als StückBeitrag (find-or-create Person, keine Dublette).
        var kname = d.KomponistName?.Trim();
        if (!string.IsNullOrWhiteSpace(kname))
        {
            var person = await PersonHolenAsync(db, kname, PersonRolleTyp.Komponist);
            var existiert = await db.StueckBeitraege.AnyAsync(b =>
                b.StueckId == stueck.Id && b.Person.Name == kname && b.Rolle == StueckRolle.Komponist);
            if (!existiert)
                db.StueckBeitraege.Add(new StueckBeitrag
                {
                    Stueck = stueck, Person = person, Rolle = StueckRolle.Komponist
                });
        }
    }

    private static async Task KomponistUebernehmenAsync(ApplicationDbContext db, string datenJson)
    {
        var d = CrawlDaten.Deserialisiere<KomponistFundDaten>(datenJson)
            ?? throw new InvalidOperationException("Komponist-Daten konnten nicht gelesen werden.");
        var name = d.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Personenname fehlt.");

        var person = await PersonHolenAsync(db, name, PersonRolleTyp.Komponist);

        // Anreicherung: nur leere Felder füllen – kuratierte Daten nicht überschreiben.
        if (string.IsNullOrWhiteSpace(person.Biografie) && !string.IsNullOrWhiteSpace(d.Biografie))
            person.Biografie = d.Biografie.Trim();
        if (string.IsNullOrWhiteSpace(person.BildUrl) && !string.IsNullOrWhiteSpace(d.BildUrl))
            person.BildUrl = d.BildUrl.Trim();
        if (person.Geburtsjahr is null && d.Geburtsjahr is { } gj)
            person.Geburtsjahr = gj;
        if (person.Wikipedia is null && !string.IsNullOrWhiteSpace(d.WikipediaUrl))
            person.Wikipedia = d.WikipediaUrl.Trim();
    }

    /// <summary>Find-or-create Person (Abgleich über Name) und stellt die gewünschte Rolle sicher.
    /// Lädt Rollen + Links mit (für Rollen-/Link-Pflege). Neue Personen sind öffentlich
    /// (Komponist:in/Dirigent:in sind Funktionsträger:innen).</summary>
    private static async Task<Person> PersonHolenAsync(ApplicationDbContext db, string name, PersonRolleTyp rolle)
    {
        name = name.Trim();
        var person = await db.Personen.Include(p => p.Rollen).Include(p => p.Links)
            .FirstOrDefaultAsync(p => p.Name == name);
        if (person == null)
        {
            person = new Person { Name = name, Sichtbarkeit = Sichtbarkeit.Oeffentlich };
            db.Personen.Add(person);
        }
        if (person.Rollen.All(r => r.Rolle != rolle))
            person.Rollen.Add(new PersonRolle { Rolle = rolle });
        return person;
    }

    private static string? Leer(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Band aus dem Fund-Namen (find-or-create) oder ersatzweise aus der Quelle ermitteln.</summary>
    private static async Task<Band?> BandAufloesenAsync(ApplicationDbContext db, CrawlFund fund, string? bandName)
    {
        bandName = bandName?.Trim();
        if (!string.IsNullOrWhiteSpace(bandName))
        {
            var band = await db.Bands.FirstOrDefaultAsync(b => b.Name == bandName);
            if (band == null)
            {
                band = new Band { Name = bandName };
                db.Bands.Add(band);
            }
            return band;
        }

        // Fallback: Ziel-Band der Quelle.
        var bandId = await db.CrawlLaeufe
            .Where(l => l.Id == fund.LaufId)
            .Select(l => l.Quelle.BandId)
            .FirstOrDefaultAsync();
        return bandId is { } id ? await db.Bands.FirstOrDefaultAsync(b => b.Id == id) : null;
    }
}
