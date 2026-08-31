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
        // Hinweis: kein Status-Guard – ein Fund darf auch erneut übernommen werden (Reaktivieren eines
        // verworfenen ODER eines bereits übernommenen Funds, z. B. wenn das Ziel im CRUD gelöscht wurde).
        // Die Übernahme-Pfade sind find-or-create → idempotent, es entstehen keine Dubletten.

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
            case CrawlFundTyp.Video:
                await VideoUebernehmenAsync(db, fund, datenJson);
                break;
            case CrawlFundTyp.StueckBeschreibung:
                await StueckBeschreibungUebernehmenAsync(db, datenJson);
                break;
            case CrawlFundTyp.Dublette:
                await DublettenUebernehmenAsync(db, datenJson);
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

    /// <summary>Setzt einen entschiedenen Fund (verworfen ODER übernommen) wieder auf „Offen" –
    /// z. B. um eine Entscheidung rückgängig zu machen.</summary>
    public static async Task WiederOeffnenAsync(ApplicationDbContext db, Guid fundId)
    {
        var fund = await db.CrawlFunde.FirstOrDefaultAsync(f => f.Id == fundId)
            ?? throw new InvalidOperationException("Fund nicht gefunden.");
        fund.Status = CrawlFundStatus.Offen;
        fund.EntschiedenAm = null;
        await db.SaveChangesAsync();
    }

    /// <summary>§4.9: setzt Beschreibung/Jahr eines BESTEHENDEN Stücks – nur LEERE Felder (kuratierte Werte bleiben).</summary>
    private static async Task StueckBeschreibungUebernehmenAsync(ApplicationDbContext db, string datenJson)
    {
        var d = CrawlDaten.Deserialisiere<StueckBeschreibungDaten>(datenJson)
            ?? throw new InvalidOperationException("Stück-Beschreibung: ungültige Daten.");
        var stueck = await db.Stuecke.FirstOrDefaultAsync(s => s.Id == d.StueckId)
            ?? throw new InvalidOperationException("Stück nicht gefunden (evtl. gelöscht/zusammengeführt).");
        if (string.IsNullOrWhiteSpace(stueck.Beschreibung) && !string.IsNullOrWhiteSpace(d.Beschreibung))
            stueck.Beschreibung = d.Beschreibung!.Trim();
        if (stueck.Jahr is null && d.Jahr is int j) stueck.Jahr = j;
        // Speichern erfolgt im Rahmen von UebernehmenAsync.
    }

    /// <summary>§4.10: führt das Dublette-Paar zusammen (Quelle → Ziel) via Merge-Service (Referenzen umhängen,
    /// Quell-Name/-Titel automatisch als Alias, Quelle löschen). Merge speichert selbst.</summary>
    private static async Task DublettenUebernehmenAsync(ApplicationDbContext db, string datenJson)
    {
        var d = CrawlDaten.Deserialisiere<DublettenDaten>(datenJson)
            ?? throw new InvalidOperationException("Dublette: ungültige Daten.");
        if (d.QuelleId == d.ZielId) throw new InvalidOperationException("Dublette: Quelle = Ziel.");
        var (ok, meldung) = d.Entitaet.ToLowerInvariant() switch
        {
            "person" => await PersonMergeService.MergeAsync(db, d.QuelleId, d.ZielId),
            "band" => await BandMergeService.MergeAsync(db, d.QuelleId, d.ZielId),
            _ => await StueckMergeService.MergeAsync(db, d.QuelleId, d.ZielId)
        };
        if (!ok) throw new InvalidOperationException($"Zusammenführen fehlgeschlagen: {meldung}");
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
            Uhrzeit: d.Uhrzeit,
            Name: d.Name,
            Ort: d.Ort,
            Beschreibung: d.Beschreibung,
            BildUrl: d.BildUrl,
            Programm: programm,
            Mitwirkende: [],
            // Offizielle Event-/Ticket-Seite: aus den Funddaten, sonst die Quell-URL des Funds (z. B. KKL-Detailseite).
            Webseite: Leer(d.Webseite) ?? Leer(fund.QuellUrl));

        // Find-or-create: derselbe (Datum,Name) wird aktualisiert statt verdoppelt (idempotenter Re-Import).
        var konzertId = await KonzertErfassungService.ErfasseOderAktualisiereAsync(db, eingabe);

        // Wettbewerbs-Rangliste (SBBW §4.2): Platzierung/Punkte je Band + Dirigent:in nachtragen.
        await RaengeUebernehmenAsync(db, konzertId, d.Raenge);
        // Zugehörige Videos (z. B. SBBW Infomaniak-VOD) anlegen und mit Stück/Band verknüpfen.
        await VideosUebernehmenAsync(db, konzertId, d.Videos);

        // Bisher angelegte KonzertBands (Programm + Ränge) persistieren, damit die folgende
        // Quell-Band-Prüfung (AnyAsync gegen die DB) die bereits getrackten Zeilen sieht und
        // die Band nicht doppelt anlegt (sonst: „instance … cannot be tracked … same key value").
        await db.SaveChangesAsync();

        // Konzert der Quell-Band zuordnen (auch ohne Programm-Band):
        //  1) reguläre BandDomain-Quelle → Quelle.BandId
        //  2) Aggregat „Konzert-Vorschau" (Quelle über ALLE Bands, BandId = null) → Band aus dem
        //     ExternKey „vorschau:{bandId}:{datum}:{name}". So wird auch ein bereits vorhandener Fund
        //     korrekt der Original-Band angehängt, wenn der Crawler keine andere Band gefunden hat.
        var bandId = await db.CrawlLaeufe
            .Where(l => l.Id == fund.LaufId)
            .Select(l => l.Quelle.BandId)
            .FirstOrDefaultAsync();
        if (bandId is null && fund.ExternKey is { } ek && ek.StartsWith("vorschau:", StringComparison.Ordinal))
        {
            var teile = ek.Split(':');
            if (teile.Length >= 2 && Guid.TryParse(teile[1], out var ausKey)) bandId = ausKey;
        }
        if (bandId is { } bid && !await db.KonzertBands.AnyAsync(kb => kb.KonzertId == konzertId && kb.BandId == bid))
            db.KonzertBands.Add(new KonzertBand { KonzertId = konzertId, BandId = bid });
    }

    /// <summary>Setzt je Rangliste-Zeile <see cref="KonzertBand.Rang"/>/<see cref="KonzertBand.Punkte"/> und
    /// trägt die Dirigentin/den Dirigenten als <see cref="KonzertPerson"/> (Rolle Dirigent) ein. Die Bands
    /// wurden bereits durch das Programm (Find-or-create) als <see cref="KonzertBand"/> angelegt.</summary>
    private static async Task RaengeUebernehmenAsync(ApplicationDbContext db, Guid konzertId,
        IReadOnlyList<RangZeileDaten>? raenge)
    {
        if (raenge is not { Count: > 0 }) return;
        await db.SaveChangesAsync(); // KonzertBands aus ErfasseAsync sichtbar machen
        var konzertBands = await db.KonzertBands.Where(kb => kb.KonzertId == konzertId).ToListAsync();

        foreach (var r in raenge)
        {
            var name = r.Band?.Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;
            var band = await db.Bands.FirstOrDefaultAsync(b => b.Name == name || b.Aliase.Any(a => a.Name == name));
            if (band == null) { band = new Band { Name = name }; db.Bands.Add(band); }

            var kb = konzertBands.FirstOrDefault(x => x.BandId == band.Id);
            if (kb == null)
            {
                kb = new KonzertBand { KonzertId = konzertId, Band = band };
                db.KonzertBands.Add(kb);
                konzertBands.Add(kb);
            }
            kb.Rang = r.Rang;
            kb.Punkte = r.Punkte;

            var dir = r.Dirigent?.Trim();
            if (!string.IsNullOrWhiteSpace(dir))
            {
                var person = await db.Personen.Include(p => p.Rollen).FirstOrDefaultAsync(p => p.Name == dir);
                if (person == null) { person = new Person { Name = dir, Sichtbarkeit = Sichtbarkeit.Oeffentlich }; db.Personen.Add(person); }
                if (person.Rollen.All(x => x.Rolle != PersonRolleTyp.Dirigent))
                    person.Rollen.Add(new PersonRolle { Rolle = PersonRolleTyp.Dirigent });
                var existiert = await db.KonzertPersonen.AnyAsync(kp =>
                    kp.KonzertId == konzertId && kp.Person.Name == dir && kp.Rolle == PersonRolleTyp.Dirigent);
                if (!existiert)
                    db.KonzertPersonen.Add(new KonzertPerson
                    {
                        KonzertId = konzertId, Person = person, Rolle = PersonRolleTyp.Dirigent, Band = band
                    });
            }
        }
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
            // Alias-fähiges Find-or-create (Normalisierung von Schreibvarianten).
            var instrument = await InstrumentService.FindeOderErstelleAsync(db, d.InstrumentName);
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

    /// <summary>Legt die Konzert-Videos an (Plattform + ExternId), verknüpft mit dem (bereits angelegten)
    /// Stück und – falls eindeutig auffindbar – der Band. Ohne passendes Stück kein Video (FK-Pflicht).
    /// Dubletten (gleiche ExternId am selben Konzert) werden übersprungen.</summary>
    private static async Task VideosUebernehmenAsync(ApplicationDbContext db, Guid konzertId,
        IReadOnlyList<KonzertVideoDaten>? videos)
    {
        if (videos is not { Count: > 0 }) return;
        await db.SaveChangesAsync(); // Stücke/Bands aus den vorigen Schritten sichtbar machen

        foreach (var v in videos)
        {
            var externId = v.ExternId?.Trim();
            if (string.IsNullOrWhiteSpace(externId)) continue;
            if (await db.Videos.AnyAsync(x => x.KonzertId == konzertId && x.Plattform == v.Plattform && x.ExternId == externId))
                continue;

            var titel = v.StueckTitel?.Trim();
            Stueck? stueck = null;
            if (!string.IsNullOrWhiteSpace(titel))
                stueck = await db.Stuecke.FirstOrDefaultAsync(s => s.Titel == titel || s.Aliase.Any(a => a.Name == titel))
                         ?? db.Stuecke.Local.FirstOrDefault(s => string.Equals(s.Titel, titel, StringComparison.OrdinalIgnoreCase))
                         ?? new Stueck { Titel = titel };
            if (stueck == null) continue; // ohne Stück kein Video
            if (db.Entry(stueck).State == EntityState.Detached) db.Stuecke.Add(stueck);

            Guid? bandId = null;
            var bn = v.Band?.Trim();
            if (!string.IsNullOrWhiteSpace(bn))
            {
                var band = await db.Bands.FirstOrDefaultAsync(b => b.Name == bn || b.Aliase.Any(a => a.Name == bn))
                           ?? db.Bands.Local.FirstOrDefault(b => string.Equals(b.Name, bn, StringComparison.OrdinalIgnoreCase));
                bandId = band?.Id; // nicht neu anlegen – Band stammt aus der Rangliste
            }

            db.Videos.Add(new Video
            {
                Plattform = v.Plattform,
                ExternId = externId,
                KonzertId = konzertId,
                Stueck = stueck,
                BandId = bandId,
                Titel = string.Join(" – ", new[] { bn, titel }.Where(x => !string.IsNullOrWhiteSpace(x))),
                Status = VideoStatus.Genehmigt
            });
        }
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

    /// <summary>Video-Fund → <c>Video</c> (Plattform YouTube). Stück ist Pflicht (find-or-create via
    /// <see cref="VideoErfassung"/>, Komponist:in optional); Ort/Anlass werden übernommen. Dublette
    /// (gleiche ExternId + Stück) wird nicht doppelt angelegt.</summary>
    private static async Task VideoUebernehmenAsync(ApplicationDbContext db, CrawlFund fund, string datenJson)
    {
        var d = CrawlDaten.Deserialisiere<VideoFundDaten>(datenJson)
            ?? throw new InvalidOperationException("Video-Daten konnten nicht gelesen werden.");
        if (string.IsNullOrWhiteSpace(d.ExternId))
            throw new InvalidOperationException("YouTube-Video-ID fehlt.");
        if (string.IsNullOrWhiteSpace(d.StueckTitel))
            throw new InvalidOperationException("Stück fehlt – bitte im Fund ergänzen, bevor er übernommen wird.");

        var stueck = await VideoErfassung.StueckHolenAsync(db, d.StueckTitel.Trim(), Leer(d.KomponistName));
        var vorhanden = await db.Videos.AnyAsync(v => v.ExternId == d.ExternId && v.StueckId == stueck.Id);
        if (!vorhanden)
            db.Videos.Add(new Video
            {
                Plattform = VideoPlattform.YouTube,
                ExternId = d.ExternId.Trim(),
                Titel = d.Titel,
                Stueck = stueck,
                BandId = d.BandId == Guid.Empty ? null : d.BandId,
                Ort = Leer(d.Ort),
                Anlass = Leer(d.Anlass),
                Status = VideoStatus.Genehmigt   // Admin-Übernahme = vertrauenswürdig
            });
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
        {
            person.BildUrl = d.BildUrl.Trim();
            person.BildAttribution = Leer(d.BildAttribution); // Quellen-/Lizenzangabe zum Bild mitübernehmen
        }
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
