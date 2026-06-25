using System.Text.RegularExpressions;
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
                await KonzertUebernehmenAsync(db, fund, datenJson);
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
            case CrawlFundTyp.Band:
                await BandUebernehmenAsync(db, fund, datenJson);
                break;
            case CrawlFundTyp.Webseite:
                await WebseiteUebernehmenAsync(db, datenJson);
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

    private static async Task KonzertUebernehmenAsync(ApplicationDbContext db, CrawlFund fund, string datenJson)
    {
        var d = CrawlDaten.Deserialisiere<KonzertFundDaten>(datenJson)
            ?? throw new InvalidOperationException("Konzert-Daten konnten nicht gelesen werden.");
        if (d.Datum is not { } datum)
            throw new InvalidOperationException("Konzert-Datum fehlt – bitte im Review ergänzen.");

        var programm = (d.Programm ?? [])
            .Where(z => !string.IsNullOrWhiteSpace(z.StueckTitel))
            .Select(z => new KonzertErfassungService.ProgrammEingabe(
                z.StueckTitel, z.KomponistName, z.BandName, z.Reihenfolge, z.ArrangeurName))
            .ToList();

        var eingabe = new KonzertErfassungService.Eingabe(
            Datum: datum,
            Name: d.Name,
            Ort: d.Ort,
            Beschreibung: d.Beschreibung,
            BildUrl: null,
            Programm: programm,
            Mitwirkende: []);

        var konzertId = await KonzertErfassungService.ErfasseAsync(db, eingabe);

        // BandDomain-Funde: Konzert immer der Quell-Band zuordnen (auch ohne Programm-Band).
        var bandId = await db.CrawlLaeufe
            .Where(l => l.Id == fund.LaufId)
            .Select(l => l.Quelle.BandId)
            .FirstOrDefaultAsync();
        if (bandId is { } bid && !await db.KonzertBands.AnyAsync(kb => kb.KonzertId == konzertId && kb.BandId == bid))
            db.KonzertBands.Add(new KonzertBand { KonzertId = konzertId, BandId = bid });
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

        var funktion = string.IsNullOrWhiteSpace(d.Funktion) ? "Dirigent" : d.Funktion.Trim();
        var istDirigent = string.Equals(funktion, "Dirigent", StringComparison.OrdinalIgnoreCase);
        // Dirigent:in → Rolle Dirigent; Vorstand/Muko & Co. → Musikant. Alle sind Funktionsträger:innen → öffentlich.
        var rolle = istDirigent ? PersonRolleTyp.Dirigent : PersonRolleTyp.Musikant;

        var person = await db.Personen.Include(p => p.Rollen).Include(p => p.Links)
            .FirstOrDefaultAsync(p => p.Name == name);
        if (person == null)
        {
            person = new Person { Name = name };
            db.Personen.Add(person);
        }
        person.Sichtbarkeit = Sichtbarkeit.Oeffentlich;
        if (person.Rollen.All(r => r.Rolle != rolle))
            person.Rollen.Add(new PersonRolle { Rolle = rolle });
        if (!string.IsNullOrWhiteSpace(d.EMail) && string.IsNullOrWhiteSpace(person.EMail))
            person.EMail = d.EMail.Trim();

        // Optionales Instrument (find-or-create) für die Mitgliedschaft.
        Guid? instrumentId = null;
        if (!string.IsNullOrWhiteSpace(d.InstrumentName))
        {
            var iname = d.InstrumentName.Trim();
            var instrument = await db.Instrumente.FirstOrDefaultAsync(i => i.Name == iname)
                             ?? db.Instrumente.Local.FirstOrDefault(i => i.Name == iname);
            if (instrument == null) { instrument = new Instrument { Name = iname }; db.Instrumente.Add(instrument); }
            instrumentId = instrument.Id;
        }

        // Keine Dublette: gleiche Person + Band + Funktion (laufende Mitgliedschaft) nicht doppelt.
        var existiert = await db.BandMitgliedschaften.AnyAsync(m =>
            m.BandId == band.Id && m.Person.Name == name && m.Funktion == funktion);
        if (!existiert)
            db.BandMitgliedschaften.Add(new BandMitgliedschaft
            {
                Band = band,
                Person = person,
                Funktion = funktion,
                InstrumentId = instrumentId,
                VonJahr = d.VonJahr,
                BisJahr = d.BisJahr
            });
    }

    private static async Task BandUebernehmenAsync(ApplicationDbContext db, CrawlFund fund, string datenJson)
    {
        var d = CrawlDaten.Deserialisiere<BandFundDaten>(datenJson)
            ?? throw new InvalidOperationException("Band-Daten konnten nicht gelesen werden.");
        var name = d.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Bandname fehlt.");

        // Ziel-Band: vorrangig die Quell-Band der Quelle, sonst find-or-create über Name/Alias.
        Band? band = null;
        var bandId = await db.CrawlLaeufe.Where(l => l.Id == fund.LaufId)
            .Select(l => l.Quelle.BandId).FirstOrDefaultAsync();
        if (bandId is { } bid)
            band = await db.Bands.Include(b => b.Links).Include(b => b.Aliase).FirstOrDefaultAsync(b => b.Id == bid);
        band ??= await db.Bands.Include(b => b.Links).Include(b => b.Aliase)
            .FirstOrDefaultAsync(b => b.Name == name || b.Aliase.Any(a => a.Name == name));
        // Auch über die Webseite matchen (z. B. die aus einem Webseiten-Fund angelegte Band).
        if (band == null && !string.IsNullOrWhiteSpace(d.Webseite)
            && Uri.TryCreate(d.Webseite, UriKind.Absolute, out var wu))
            band = await db.Bands.Include(b => b.Links).Include(b => b.Aliase)
                .FirstOrDefaultAsync(b => b.Webseite != null && b.Webseite.Contains(wu.Host));
        if (band == null)
        {
            band = new Band { Name = name };
            db.Bands.Add(band);
        }

        // Nur leere Felder füllen – kuratierte Daten nicht überschreiben.
        if (string.IsNullOrWhiteSpace(band.Land)) band.Land = Leer(d.Land) ?? band.Land;
        if (string.IsNullOrWhiteSpace(band.Webseite)) band.Webseite = Leer(d.Webseite) ?? band.Webseite;
        if (string.IsNullOrWhiteSpace(band.BildUrl)) band.BildUrl = Leer(d.BildUrl) ?? band.BildUrl;
        if (string.IsNullOrWhiteSpace(band.Geschichte)) band.Geschichte = Leer(d.Geschichte) ?? band.Geschichte;
        band.Kategorie ??= d.Kategorie;
        band.Staerkeklasse ??= d.Staerkeklasse;
        band.Gruendungsjahr ??= d.Gruendungsjahr;

        // Social-Links (Komfort-Setter; nur setzen, wenn noch leer).
        if (band.Instagram is null) band.Instagram = Leer(d.Instagram);
        if (band.Facebook is null) band.Facebook = Leer(d.Facebook);
        if (band.YouTube is null) band.YouTube = Leer(d.YouTube);
        if (band.X is null) band.X = Leer(d.X);
        if (band.Wikipedia is null) band.Wikipedia = Leer(d.Wikipedia);
        if (band.EMail is null) band.EMail = Leer(d.EMail);
        if (band.Mobile is null) band.Mobile = Leer(d.Mobile);

        // Aliase ergänzen (inkl. abweichendem Fund-Namen), ohne Dubletten.
        AliasErgaenzen(band, name);
        foreach (var a in d.Aliase ?? []) AliasErgaenzen(band, a);
    }

    private static async Task WebseiteUebernehmenAsync(ApplicationDbContext db, string datenJson)
    {
        var d = CrawlDaten.Deserialisiere<WebseiteFundDaten>(datenJson)
            ?? throw new InvalidOperationException("Webseiten-Daten konnten nicht gelesen werden.");
        if (string.IsNullOrWhiteSpace(d.Url) || !Uri.TryCreate(d.Url.Trim(), UriKind.Absolute, out var uri))
            throw new InvalidOperationException("Ungültige Webseiten-URL.");

        var host = uri.Host;
        var root = $"{uri.Scheme}://{host}/";

        // Ziel-Band find-or-create (über Webseite-Host, sonst Name) + Kategorie/Klasse aus dem Fund.
        var name = SauberBandName(d.VereinName, host);
        var band = await db.Bands.FirstOrDefaultAsync(b => b.Webseite != null && b.Webseite.Contains(host))
                   ?? await db.Bands.FirstOrDefaultAsync(b => b.Name == name);
        if (band == null)
        {
            band = new Band { Name = name };
            db.Bands.Add(band);
        }
        if (string.IsNullOrWhiteSpace(band.Webseite)) band.Webseite = root;
        var (kat, klasse) = ParseKategorieKlasse(d.Kategorie);
        band.Kategorie ??= kat;
        band.Staerkeklasse ??= klasse;

        // Quelle (Vorschlag) anlegen mit gesetzter Ziel-Band; existiert sie schon, Ziel-Band nachtragen.
        var quelle = await db.CrawlQuellen.FirstOrDefaultAsync(q => q.Domain == host);
        if (quelle == null)
            db.CrawlQuellen.Add(new CrawlQuelle
            {
                Typ = CrawlQuelleTyp.BandDomain,
                StartUrl = root,
                Domain = host,
                Aktiv = false, // Vorschlag – Admin aktiviert und startet den Lauf
                BandId = band.Id
            });
        else if (quelle.BandId == null)
            quelle.BandId = band.Id;
    }

    /// <summary>Bestmöglicher Band-Name aus dem Seitentitel (Trenner/„Home"-Wörter entfernt), sonst aus der Domain.</summary>
    private static string SauberBandName(string? titel, string host)
    {
        var t = (titel ?? "").Trim();
        if (t.Length > 0)
        {
            var segmente = Regex.Split(t, @"\s*[|–—·:]\s*|\s+-\s+")
                .Select(s => s.Trim())
                .Where(s => s.Length >= 3 &&
                            !Regex.IsMatch(s, "^(home|start(seite)?|willkommen|welcome|aktuelles?|news)$", RegexOptions.IgnoreCase))
                .ToList();
            if (segmente.Count > 0) return segmente.OrderByDescending(s => s.Length).First();
        }
        var h = host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
        var basis = h.Split('.')[0].Replace('-', ' ').Replace('_', ' ');
        return System.Globalization.CultureInfo.GetCultureInfo("de-CH").TextInfo.ToTitleCase(basis);
    }

    /// <summary>Parst eine Kategorie-Zeichenkette (z. B. „Konzertmusik, Höchstklasse, Harmonie") in die Enums.</summary>
    private static (BandKategorie?, Staerkeklasse?) ParseKategorieKlasse(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return (null, null);
        var t = s.ToLowerInvariant();
        BandKategorie? kat =
            t.Contains("brass") ? BandKategorie.Brassband
            : t.Contains("harmonie") ? BandKategorie.Harmonie
            : t.Contains("fanfare") ? BandKategorie.Fanfare
            : null;
        Staerkeklasse? klasse =
            t.Contains("höchst") || t.Contains("hoechst") ? Staerkeklasse.Hoechstklasse
            : t.Contains("elite") ? Staerkeklasse.Elite
            : Regex.IsMatch(t, @"1\.?\s*klasse") ? Staerkeklasse.Klasse1
            : Regex.IsMatch(t, @"2\.?\s*klasse") ? Staerkeklasse.Klasse2
            : Regex.IsMatch(t, @"3\.?\s*klasse") ? Staerkeklasse.Klasse3
            : Regex.IsMatch(t, @"4\.?\s*klasse") ? Staerkeklasse.Klasse4
            : null;
        return (kat, klasse);
    }

    private static void AliasErgaenzen(Band band, string? alias)
    {
        alias = alias?.Trim();
        if (string.IsNullOrWhiteSpace(alias)) return;
        if (string.Equals(alias, band.Name, StringComparison.OrdinalIgnoreCase)) return;
        if (band.Aliase.Any(a => string.Equals(a.Name, alias, StringComparison.OrdinalIgnoreCase))) return;
        band.Aliase.Add(new BandAlias { BandId = band.Id, Name = alias });
    }

    private static async Task StueckUebernehmenAsync(ApplicationDbContext db, CrawlFund fund, string datenJson)
    {
        var d = CrawlDaten.Deserialisiere<StueckFundDaten>(datenJson)
            ?? throw new InvalidOperationException("Stück-Daten konnten nicht gelesen werden.");
        var titel = d.Titel?.Trim();
        if (string.IsNullOrWhiteSpace(titel))
            throw new InvalidOperationException("Stück-Titel fehlt.");

        // Find-or-create Stück (Abgleich über Titel oder Alias).
        var stueck = await db.Stuecke.Include(s => s.Aliase)
            .FirstOrDefaultAsync(s => s.Titel == titel || s.Aliase.Any(a => a.Name == titel));
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

        // Komponist:in-/Arrangeur:in-Feld zerlegen (mehrere Namen, Arr.-Marker → Arrangeur) und
        // je Beitrag find-or-create Person, keine Dublette (Stück + Person + Rolle).
        foreach (var beitrag in KomponistParser.Parse(d.KomponistName))
        {
            var person = await PersonHolenAsync(db, beitrag.Name, PersonRolleTyp.Komponist);
            var existiert = await db.StueckBeitraege.AnyAsync(b =>
                b.StueckId == stueck.Id && b.Person.Name == beitrag.Name && b.Rolle == beitrag.Rolle);
            if (!existiert)
                db.StueckBeitraege.Add(new StueckBeitrag
                {
                    Stueck = stueck, Person = person, Rolle = beitrag.Rolle
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
