using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services.Crawler;

/// <summary>
/// Führt einen einzelnen <see cref="CrawlLauf"/> aus (Spec §4-Pipeline): Fetch → Seiten-Filter →
/// Extraktion → <see cref="CrawlFund"/>. Für <see cref="CrawlQuelleTyp.BandDomain"/> ein
/// domain-begrenzter BFS mit Tiefen-/Seitenlimit; für Dokument/Event ein Einzelabruf.
/// Dedup/Politeness über <see cref="CrawlSeite"/>. Scoped – ein Lauf pro Instanz.
/// </summary>
public class CrawlRunner(
    ApplicationDbContext db,
    CrawlFetchService fetch,
    IExtraktion extraktion,
    KomponistSuche komponistSuche,
    ISeitenRenderer renderer,
    ILogger<CrawlRunner> logger)
{
    private string? _bandName;
    private string? _hinweis;
    // Dedup innerhalb eines Laufs: je Fund-Identität der bisher vollständigste Datensatz.
    private readonly Dictionary<string, (CrawlFund Fund, int Score)> _gesehen = new();
    // Im Lauf gefundene Gremiums-Mitglieder (Name|Funktion) – für die Abgangs-Prüfung.
    private readonly HashSet<string> _boardGesehen = new(StringComparer.OrdinalIgnoreCase);

    public async Task AusfuehrenAsync(Guid laufId, CancellationToken ct)
    {
        var lauf = await db.CrawlLaeufe.FirstOrDefaultAsync(l => l.Id == laufId, ct);
        if (lauf == null) return;
        var quelle = await db.CrawlQuellen.FirstOrDefaultAsync(q => q.Id == lauf.QuelleId, ct);
        if (quelle == null)
        {
            lauf.Status = CrawlLaufStatus.Fehler;
            lauf.Meldung = "Quelle nicht gefunden.";
            lauf.EndeAm = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        lauf.Status = CrawlLaufStatus.Laufend;
        _gesehen.Clear();
        _boardGesehen.Clear();
        await db.SaveChangesAsync(ct);

        // Kontext für die Extraktion: Quell-Band (für BandDomain-Zuordnung) + Admin-Hinweis.
        _hinweis = quelle.ExtraktionsHinweis;

        // BandDomain ohne Ziel-Band: Band aus der Domain bestimmen/anlegen, damit gefundene Personen
        // (Dirigent/Vorstand/Muko) und Konzerte „tendenziell der Band der Seite" zugeordnet werden.
        if (quelle.Typ == CrawlQuelleTyp.BandDomain && quelle.BandId is null
            && Uri.TryCreate(quelle.StartUrl, UriKind.Absolute, out var su))
        {
            var host = su.Host;
            var band = await db.Bands.FirstOrDefaultAsync(b => b.Webseite != null && b.Webseite.Contains(host), ct);
            if (band == null)
            {
                band = new Band { Name = BandNameAusHost(host), Webseite = $"{su.Scheme}://{host}/" };
                db.Bands.Add(band);
            }
            quelle.BandId = band.Id;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("BandDomain ohne Ziel-Band → Band '{Name}' zugeordnet.", band.Name);
        }

        _bandName = quelle.BandId is { } bid
            ? await db.Bands.Where(b => b.Id == bid).Select(b => b.Name).FirstOrDefaultAsync(ct)
            : null;

        logger.LogInformation("▶ Crawl-Lauf {LaufId} gestartet: Typ={Typ}, Start={Url}{Band}{Hinweis}",
            lauf.Id, quelle.Typ, quelle.StartUrl,
            _bandName != null ? $", Band={_bandName}" : "",
            string.IsNullOrWhiteSpace(_hinweis) ? "" : $", Hinweis=\"{_hinweis}\"");

        try
        {
            if (quelle.Typ == CrawlQuelleTyp.BandDomain)
                await BandDomainCrawlAsync(lauf, quelle, ct);
            else if (quelle.Typ == CrawlQuelleTyp.Wettbewerb || SbbwImporter.IstZustaendig(quelle.StartUrl))
                await SbbwImportierenAsync(lauf, quelle, ct);
            else if (quelle.Typ == CrawlQuelleTyp.Veranstalter || KklImporter.IstZustaendig(quelle.StartUrl))
                await KklImportierenAsync(lauf, quelle, ct);
            else if (EmfVereinImporter.IstZustaendig(quelle.StartUrl))
                await EmfVereineImportierenAsync(lauf, quelle, ct);
            else
                await EinzelAbrufAsync(lauf, quelle, quelle.StartUrl, einzelseiteImmerRelevant: true, ct);

            // Abgänge im Gremium nur prüfen, wenn ein Gremium gecrawlt wurde UND tatsächlich Mitglieder
            // gefunden wurden (sonst würde ein Fehl-/Leerlauf fälschlich alle als „weg" melden).
            var boardGewuenscht = quelle.Anforderungen.HasFlag(CrawlAnforderungen.VorstandCrawlen)
                                  || quelle.Anforderungen.HasFlag(CrawlAnforderungen.MukoCrawlen);
            if (boardGewuenscht && quelle.BandId is not null && _boardGesehen.Count > 0)
                await AbgaengePruefenAsync(lauf, quelle.BandId.Value, ct);

            lauf.Status = CrawlLaufStatus.Fertig;
            lauf.Meldung = $"{lauf.SeitenBesucht} Seiten, {lauf.FundeAnzahl} Funde.";
            logger.LogInformation("■ Crawl-Lauf {LaufId} fertig: {Seiten} Seiten, {Funde} Funde.",
                lauf.Id, lauf.SeitenBesucht, lauf.FundeAnzahl);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            lauf.Status = CrawlLaufStatus.Abgebrochen;
            lauf.Meldung = "Abgebrochen.";
        }

        lauf.EndeAm = DateTime.UtcNow;
        quelle.LetzterLaufAm = lauf.EndeAm;
        await db.SaveChangesAsync(ct);
    }

    private async Task BandDomainCrawlAsync(CrawlLauf lauf, CrawlQuelle quelle, CancellationToken ct)
    {
        if (!Uri.TryCreate(quelle.StartUrl, UriKind.Absolute, out var start))
            throw new InvalidOperationException("Ungültige Start-URL.");

        var besucht = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var frontier = new Queue<(string Url, int Tiefe)>();
        frontier.Enqueue((start.GetLeftPart(UriPartial.Query), 0));

        while (frontier.Count > 0 && lauf.SeitenBesucht < quelle.MaxSeiten)
        {
            ct.ThrowIfCancellationRequested();
            var (url, tiefe) = frontier.Dequeue();
            if (!besucht.Add(url)) continue;

            var links = await EinzelAbrufAsync(lauf, quelle, url, einzelseiteImmerRelevant: false, ct);

            if (tiefe < quelle.MaxTiefe)
                foreach (var l in links)
                    if (!besucht.Contains(l))
                        frontier.Enqueue((l, tiefe + 1));
        }
    }

    /// <summary>Lädt eine Seite, dedupliziert, filtert, extrahiert und gibt interne Links zurück
    /// (nur bei HTML; bei PDF leer). Speichert nach jeder Seite, damit das Log live mitläuft.</summary>
    private async Task<List<string>> EinzelAbrufAsync(
        CrawlLauf lauf, CrawlQuelle quelle, string url, bool einzelseiteImmerRelevant, CancellationToken ct)
    {
        // Event-Quellen sind i. d. R. SPAs → immer rendern (sofern global aktiv); sonst nach Flag.
        var rendern = quelle.BrauchtRendering || quelle.Typ == CrawlQuelleTyp.Event;
        var fundeVorher = lauf.FundeAnzahl;
        var res = await fetch.HoleAsync(url, rendern, ct);
        lauf.SeitenBesucht++;

        if (!res.Erfolg)
        {
            await SeiteMerkenAsync(quelle.Id, url, null, relevant: false, ct);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("✗ {Url}: Abruf fehlgeschlagen – {Fehler}", url, res.Fehler);
            return [];
        }

        var text = res.IstPdf ? (res.Text ?? "") : CrawlHtmlHelfer.TextBereinigen(res.Text ?? "");
        var istHtml = !res.IstPdf && !string.IsNullOrEmpty(res.Text);
        var links = istHtml ? CrawlHtmlHelfer.InterneLinks(res.Text!, new Uri(url)) : new List<string>();
        // Logo-Kandidat aus dem HTML (für Band-Funde) – aus dem Text allein nicht ableitbar.
        var logo = istHtml ? CrawlHtmlHelfer.LogoUrl(res.Text!, new Uri(url)) : null;

        var relevant = einzelseiteImmerRelevant || SeitenFilter.IstRelevant(url, text);

        var unveraendert = await SeiteMerkenAsync(quelle.Id, url, res.InhaltsHash, relevant, ct);

        if (relevant && !unveraendert && text.Trim().Length > 0)
        {
            var erg = await extraktion.ExtrahiereAsync(
                new ExtraktionsAnfrage(quelle.Typ, url, text, res.IstPdf, _bandName, _hinweis, logo,
                    quelle.Anforderungen.HasFlag(CrawlAnforderungen.VorstandCrawlen),
                    quelle.Anforderungen.HasFlag(CrawlAnforderungen.MukoCrawlen)), ct);

            foreach (var f in erg.Funde)
            {
                if (!AnforderungErfuellt(f, quelle)) continue;
                BoardMerken(f);
                var (key, score) = Bewerten(f);

                // Bereits gesehener Fund (gleiche Identität): nur ersetzen, wenn vollständiger.
                if (key != null && _gesehen.TryGetValue(key, out var vorhanden))
                {
                    if (score > vorhanden.Score)
                    {
                        vorhanden.Fund.DatenJson = f.DatenJson;
                        vorhanden.Fund.Konfidenz = f.Konfidenz;
                        vorhanden.Fund.QuellUrl = url;
                        vorhanden.Fund.AbgerufenAm = DateTime.UtcNow;
                        _gesehen[key] = (vorhanden.Fund, score);
                    }
                    continue; // kein neuer Fund
                }

                var neu = new CrawlFund
                {
                    LaufId = lauf.Id,
                    Typ = f.Typ,
                    QuellUrl = url,
                    AbgerufenAm = DateTime.UtcNow,
                    DatenJson = f.DatenJson,
                    Konfidenz = f.Konfidenz,
                    Status = CrawlFundStatus.Offen
                };
                db.CrawlFunde.Add(neu);
                lauf.FundeAnzahl++;
                if (key != null) _gesehen[key] = (neu, score);
            }
        }

        logger.LogInformation("• {Url}: {Render}, {Zeichen} Zeichen, relevant={Relevant}, {Funde} neue Funde, {Links} interne Links",
            url, res.Gerendert ? "gerendert" : "HTTP", text.Length, relevant, lauf.FundeAnzahl - fundeVorher, links.Count);

        // Vereins-Link-Ernte: bei Event-Quellen ausgehende Links zu fremden Domains als
        // Webseiten-FUNDE (mit Mini-Vorschau) anlegen – der Admin entscheidet je Fund; beim Übernehmen
        // entsteht eine inaktive BandDomain-Quelle (Vorschlag). Spec §4.1 C2.
        if (quelle.Typ == CrawlQuelleTyp.Event && istHtml)
            await VereinsLinksErntenAsync(lauf, res.Text!, url, ct);

        await db.SaveChangesAsync(ct);
        return links;
    }

    /// <summary>Erntet fremde Domains einer Event-Seite, lädt je eine kleine Vorschau (Titel/Beschreibung,
    /// ohne LLM, parallel) und legt je neue Domain einen <see cref="CrawlFundTyp.Webseite"/>-Fund an.
    /// Schon bekannte Domains (bestehende Quellen) werden übersprungen.</summary>
    private async Task VereinsLinksErntenAsync(CrawlLauf lauf, string html, string url, CancellationToken ct)
    {
        const int maxVorschlaege = 500;
        // Vereins-Domains MIT Kategorie/Klasse (aus den Gruppen-Überschriften der Verzeichnis-Seite).
        var kandidaten = CrawlHtmlHelfer.ExterneLinksMitKategorie(html, new Uri(url));

        // Schon bekannte Domains (bestehende Quellen) überspringen.
        var bekannt = (await db.CrawlQuellen.Where(q => q.Domain != null).Select(q => q.Domain!).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        kandidaten = kandidaten.Where(k => !bekannt.Contains(new Uri(k.Url).Host)).ToList();
        if (kandidaten.Count == 0)
        {
            logger.LogInformation("Vereins-Link-Ernte {Url}: keine neuen Domains.", url);
            return;
        }

        // Liegt ein Hinweis vor, vom LLM nach Kategorie/Klasse filtern (z. B. 'Höchstklasse, Harmonie').
        if (!string.IsNullOrWhiteSpace(_hinweis))
        {
            var passende = await extraktion.FiltereVereineAsync(
                kandidaten.Select(k => new VereinKandidat(k.Url, k.Kategorie)).ToList(), _hinweis!, ct);
            var set = passende.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var vorher = kandidaten.Count;
            kandidaten = kandidaten.Where(k => set.Contains(k.Url)).ToList();
            logger.LogInformation("Vereins-Link-Ernte {Url}: {Vorher} neue Domains → {Nachher} nach Hinweis-Filter '{Hinweis}'.",
                url, vorher, kandidaten.Count, _hinweis);
        }
        else
        {
            logger.LogInformation("Vereins-Link-Ernte {Url}: {Neu} neue Domains (kein Hinweis-Filter).", url, kandidaten.Count);
        }

        if (kandidaten.Count > maxVorschlaege)
        {
            logger.LogWarning("{Total} Treffer – nur {Max} werden angelegt.", kandidaten.Count, maxVorschlaege);
            kandidaten = kandidaten.Take(maxVorschlaege).ToList();
        }
        if (kandidaten.Count == 0) return;

        // Vorschau parallel laden (verschiedene Domains; DB-frei – EF ist nicht thread-safe).
        var drossel = new SemaphoreSlim(8);
        var previews = await Task.WhenAll(kandidaten.Select(async k =>
        {
            await drossel.WaitAsync(ct);
            try
            {
                var host = new Uri(k.Url).Host;
                var r = await fetch.HoleAsync(k.Url, false, ct);
                string? titel = null, besch = null;
                if (r.Erfolg && !string.IsNullOrEmpty(r.Text))
                    (titel, besch) = CrawlHtmlHelfer.SeitenInfo(r.Text!);
                return new WebseiteFundDaten(k.Url, titel ?? host, titel, besch, k.Kategorie);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { return new WebseiteFundDaten(k.Url, new Uri(k.Url).Host, Kategorie: k.Kategorie); }
            finally { drossel.Release(); }
        }));

        // Funde sequenziell schreiben (ein DbContext).
        foreach (var p in previews)
        {
            db.CrawlFunde.Add(new CrawlFund
            {
                LaufId = lauf.Id,
                Typ = CrawlFundTyp.Webseite,
                QuellUrl = p.Url,
                AbgerufenAm = DateTime.UtcNow,
                DatenJson = CrawlDaten.Serialisiere(p),
                Status = CrawlFundStatus.Offen
            });
            lauf.FundeAnzahl++;
        }
        logger.LogInformation("{Count} Webseiten-Funde aus {Url} angelegt.", previews.Length, url);
    }

    /// <summary>EMF-Vereinsverzeichnis: Vereine aus der JSON-API holen (kein Rendering → kein OOM),
    /// nach Hinweis filtern und je Verein mit Website einen <see cref="CrawlFundTyp.Webseite"/>-Fund
    /// anlegen. Schlägt die API fehl, Fallback auf den normalen Seiten-Abruf.</summary>
    private async Task EmfVereineImportierenAsync(CrawlLauf lauf, CrawlQuelle quelle, CancellationToken ct)
    {
        logger.LogInformation("EMF-Vereinsverzeichnis erkannt → JSON-API statt Rendering: {Api}", EmfVereinImporter.ApiUrl);
        var res = await fetch.HoleAsync(EmfVereinImporter.ApiUrl, false, ct);
        lauf.SeitenBesucht++;

        List<EmfVereinImporter.Verein>? vereine = null;
        if (res.Erfolg && !string.IsNullOrWhiteSpace(res.Text))
            try { vereine = EmfVereinImporter.Parse(res.Text!); }
            catch (Exception ex) { logger.LogWarning(ex, "EMF-API-JSON nicht lesbar."); }

        if (vereine is null)
        {
            logger.LogWarning("EMF-API nicht nutzbar ({Fehler}) – Fallback auf Seiten-Crawl.", res.Fehler ?? "JSON-Fehler");
            await EinzelAbrufAsync(lauf, quelle, quelle.StartUrl, einzelseiteImmerRelevant: true, ct);
            return;
        }

        var gesamt = vereine.Count;
        if (!string.IsNullOrWhiteSpace(_hinweis))
            vereine = vereine.Where(v => EmfVereinImporter.PasstZuHinweis(v.kategorie, _hinweis)).ToList();

        // Schon bekannte Domains (bestehende Quellen) überspringen.
        var bekannt = (await db.CrawlQuellen.Where(q => q.Domain != null).Select(q => q.Domain!).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int angelegt = 0, ohneWebsite = 0;
        foreach (var v in vereine)
        {
            if (string.IsNullOrWhiteSpace(v.website)
                || !Uri.TryCreate(v.website.Trim(), UriKind.Absolute, out var wu)
                || (wu.Scheme != Uri.UriSchemeHttp && wu.Scheme != Uri.UriSchemeHttps))
            { ohneWebsite++; continue; }
            if (!bekannt.Add(wu.Host)) continue; // bekannt oder schon im Lauf angelegt

            var daten = new WebseiteFundDaten(
                $"{wu.Scheme}://{wu.Host}/", v.name ?? wu.Host, v.name, null, v.kategorie);
            db.CrawlFunde.Add(new CrawlFund
            {
                LaufId = lauf.Id,
                Typ = CrawlFundTyp.Webseite,
                QuellUrl = daten.Url,
                AbgerufenAm = DateTime.UtcNow,
                DatenJson = CrawlDaten.Serialisiere(daten),
                Status = CrawlFundStatus.Offen
            });
            lauf.FundeAnzahl++;
            angelegt++;
        }

        logger.LogInformation(
            "EMF-API: {Gesamt} Vereine, {Gefiltert} nach Hinweis '{Hinweis}', {Angelegt} Webseiten-Funde ({Ohne} ohne Website).",
            gesamt, vereine.Count, _hinweis ?? "—", angelegt, ohneWebsite);

        await db.SaveChangesAsync(ct);
    }

    /// <summary>SBBW (§4.2): Jahres-Ergebnis-PDF(s) holen, je Kategorie via LLM zur Rangliste strukturieren
    /// und je (Jahr, Kategorie) einen Konzert-Fund (Datum, Aufgabestück, Rang/Band/Dirigent) anlegen.
    /// Zusätzlich (Teil 2b) die Infomaniak-Videos der Video-Unterseiten zuordnen und mitführen.</summary>
    private async Task SbbwImportierenAsync(CrawlLauf lauf, CrawlQuelle quelle, CancellationToken ct)
    {
        // Komponist-Suche je Titel zwischenspeichern (mehrere Bands spielen evtl. dasselbe Selbstwahlstück).
        var komponistCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        // PDF-URLs bestimmen: direkte Jahres-PDF-URL ODER die Resultate-Übersicht (verlinkt die PDFs).
        List<string> pdfUrls;
        if (SbbwImporter.JahrAusUrl(quelle.StartUrl) is not null)
            pdfUrls = [quelle.StartUrl];
        else
        {
            var idx = await fetch.HoleAsync(quelle.StartUrl, false, ct);
            lauf.SeitenBesucht++;
            pdfUrls = idx.Erfolg && !string.IsNullOrEmpty(idx.Text)
                ? SbbwImporter.PdfLinks(idx.Text!, new Uri(quelle.StartUrl))
                : [];
            logger.LogInformation("SBBW: {Anzahl} Jahres-PDF(s) gefunden.", pdfUrls.Count);
            if (pdfUrls.Count == 0) return;
        }

        foreach (var pdfUrl in pdfUrls)
        {
            ct.ThrowIfCancellationRequested();
            var jahr = SbbwImporter.JahrAusUrl(pdfUrl);
            var res = await fetch.HoleAsync(pdfUrl, false, ct);
            lauf.SeitenBesucht++;
            if (!res.Erfolg || !res.IstPdf || string.IsNullOrWhiteSpace(res.Text))
            {
                logger.LogWarning("SBBW: PDF nicht lesbar: {Url} ({Fehler})", pdfUrl, res.Fehler ?? "kein Text");
                continue;
            }

            var rangliste = await extraktion.SbbwRanglisteAsync(res.Text!, ct);
            var kategorien = rangliste?.Kategorien ?? [];
            if (kategorien.Count == 0)
            {
                logger.LogWarning("SBBW: keine Kategorien aus {Url} extrahiert.", pdfUrl);
                continue;
            }

            // Videos des Jahres (3 Unterseiten) holen + je Kategorie zuordnen (Teil 2b).
            var videosProKat = await SbbwVideosFuerJahrAsync(pdfUrl, jahr, ct);

            var fundeVorher = lauf.FundeAnzahl;
            foreach (var kat in kategorien)
            {
                var zeilen = kat.Zeilen ?? [];
                var programm = new List<ProgrammZeileDaten>();
                var raenge = new List<RangZeileDaten>();
                foreach (var z in zeilen)
                {
                    if (string.IsNullOrWhiteSpace(z.Band)) continue;
                    var band = z.Band!.Trim();
                    if (!string.IsNullOrWhiteSpace(kat.AufgabestueckTitel))
                        programm.Add(new ProgrammZeileDaten(kat.AufgabestueckTitel!.Trim(),
                            Leer2(kat.AufgabestueckKomponist), band, z.Rang));
                    if (!string.IsNullOrWhiteSpace(z.SelbstwahlTitel))
                    {
                        var swTitel = z.SelbstwahlTitel!.Trim();
                        // Komponist:in fehlt im PDF → per Web-Suche + LLM best-effort ergänzen (gecacht).
                        var swKomp = Leer2(z.SelbstwahlKomponist)
                                     ?? await KomponistFuerAsync(swTitel, komponistCache, ct);
                        programm.Add(new ProgrammZeileDaten(swTitel, swKomp, band, z.Rang));
                    }
                    raenge.Add(new RangZeileDaten(band, z.Rang, z.Punkte, Leer2(z.Dirigent), Leer2(z.Kanton)));
                }
                if (raenge.Count == 0) continue;

                // Videos dieser Kategorie zuordnen: Aufgabe-Videos → Aufgabestück-Titel (autoritativ aus
                // dem PDF), Selbstwahl-Videos → der vom LLM gelesene Titel; Band best-effort.
                var videos = new List<KonzertVideoDaten>();
                if (videosProKat.TryGetValue(KatKey(kat.Kategorie), out var vids))
                    foreach (var v in vids)
                    {
                        if (string.IsNullOrWhiteSpace(v.Id)) continue;
                        var istAufgabe = string.Equals(v.StueckTyp, "Aufgabe", StringComparison.OrdinalIgnoreCase);
                        var titel = istAufgabe ? Leer2(kat.AufgabestueckTitel) ?? Leer2(v.StueckTitel)
                                               : Leer2(v.StueckTitel);
                        videos.Add(new KonzertVideoDaten(
                            HarmoniQ.Web.Data.Models.VideoPlattform.InfomaniakVod, v.Id!.Trim(),
                            Leer2(v.Band), titel));
                    }

                var name = $"SBBW {jahr?.ToString() ?? ""} – {kat.Kategorie}".Replace("  ", " ").Trim();
                var daten = new KonzertFundDaten(
                    Datum: kat.Datum,
                    Name: name,
                    Ort: Leer2(kat.Ort),
                    Programm: programm,
                    Raenge: raenge,
                    Videos: videos.Count > 0 ? videos : null);

                db.CrawlFunde.Add(new CrawlFund
                {
                    LaufId = lauf.Id,
                    Typ = CrawlFundTyp.Konzert,
                    QuellUrl = pdfUrl,
                    AbgerufenAm = DateTime.UtcNow,
                    DatenJson = CrawlDaten.Serialisiere(daten),
                    Konfidenz = Konfidenz.Mittel,
                    Status = CrawlFundStatus.Offen
                });
                lauf.FundeAnzahl++;
            }

            logger.LogInformation("SBBW {Jahr}: {Kat} Kategorien, {Funde} Konzert-Funde aus {Url}.",
                jahr, kategorien.Count, lauf.FundeAnzahl - fundeVorher, pdfUrl);
            await db.SaveChangesAsync(ct);
        }
    }

    private static string? Leer2(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Komponist:in zu einem Stück-Titel ermitteln (Web-Suche + LLM), je Titel gecacht.</summary>
    private async Task<string?> KomponistFuerAsync(string titel, Dictionary<string, string?> cache, CancellationToken ct)
    {
        if (cache.TryGetValue(titel, out var vorhanden)) return vorhanden;
        var name = await komponistSuche.KomponistAsync(titel, ct);
        cache[titel] = name;
        return name;
    }

    /// <summary>Holt die 3 SBBW-Video-Unterseiten eines Jahres (ch-elite / 1st-2nd / 3rd-4th), lässt sie
    /// vom LLM zu (Video → Kategorie/Band/Stück) auswerten und gruppiert das Ergebnis je Kategorie.</summary>
    private async Task<Dictionary<string, List<SbbwVideo>>> SbbwVideosFuerJahrAsync(
        string pdfUrl, int? jahr, CancellationToken ct)
    {
        var proKat = new Dictionary<string, List<SbbwVideo>>();
        if (jahr is null || !Uri.TryCreate(pdfUrl, UriKind.Absolute, out var u)) return proKat;

        foreach (var suffix in new[] { "ch-elite", "1st-2nd", "3rd-4th" })
        {
            ct.ThrowIfCancellationRequested();
            var url = $"{u.Scheme}://{u.Host}/{jahr}-{suffix}";
            var res = await fetch.HoleAsync(url, false, ct);
            if (!res.Erfolg || string.IsNullOrWhiteSpace(res.Text)) continue;

            var outline = CrawlHtmlHelfer.VideoSeiteOutline(res.Text!);
            var videos = await extraktion.SbbwVideosAsync(outline, ct);
            foreach (var v in videos)
            {
                if (string.IsNullOrWhiteSpace(v.Id)) continue;
                var key = KatKey(v.Kategorie);
                if (key.Length == 0) continue;
                (proKat.TryGetValue(key, out var list) ? list : proKat[key] = []).Add(v);
            }
        }
        var gesamt = proKat.Values.Sum(l => l.Count);
        if (gesamt > 0) logger.LogInformation("SBBW {Jahr}: {Anzahl} Videos zugeordnet.", jahr, gesamt);
        return proKat;
    }

    /// <summary>Kanonischer Schlüssel für eine SBBW-Kategorie (Höchst/Excellence, Elite, 1.–4. Kat.).</summary>
    private static string KatKey(string? s)
    {
        var t = (s ?? "").ToLowerInvariant();
        if (t.Contains("höchst") || t.Contains("hoechst") || t.Contains("excellence")) return "hoechst";
        if (t.Contains("elite")) return "elite";
        foreach (var (z, k) in new[] { ("1", "k1"), ("2", "k2"), ("3", "k3"), ("4", "k4") })
            if (t.Contains(z + ".") || t.StartsWith(z + " ") || t.Contains(z + "e ") || t.Contains(z + "st")
                || t.Contains(z + "nd") || t.Contains(z + "rd") || t.Contains(z + "th")) return k;
        return "";
    }

    /// <summary>KKL/Veranstalter (§4.3): Eventliste rendern + vivenu-API-Antworten mitschneiden. Passt der
    /// Stil-Hinweis zu einer KKL-Kategorie, filtert die Website selbst (<c>?genre=</c>), sonst filtert der
    /// LLM. Je passendem Event wird die <b>Detailseite gerendert</b> (Tabs „Programm"/„Mitwirkende") und per
    /// LLM in Stücke + Band + Dirigent:in strukturiert. Dedup über Läufe via vivenu-Event-ID (<see cref="CrawlFund.ExternKey"/>).</summary>
    private async Task KklImportierenAsync(CrawlLauf lauf, CrawlQuelle quelle, CancellationToken ct)
    {
        var basisUrl = string.IsNullOrWhiteSpace(quelle.StartUrl) ? KklImporter.EventsUrl : quelle.StartUrl;
        var genre = KklImporter.GenreAusHinweis(_hinweis);   // Hinweis → KKL-Kategorie (sonst null = LLM-Filter)
        var startUrl = KklImporter.ListeUrl(basisUrl, genre);
        logger.LogInformation("KKL/Veranstalter → Eventliste rendern (Kategorie: {Genre}): {Url}",
            genre ?? "(LLM-Filter)", startUrl);
        // Liste rendern: vivenu-Event-JSONs + die echten KKL-Detail-Links („/events/…") mitschneiden.
        var sammlung = await renderer.RenderUndSammleAsync(startUrl, KklImporter.VivenuApiFilter, "/events/", ct);
        lauf.SeitenBesucht++;
        if (sammlung.ApiKoerper.Count == 0)
        {
            logger.LogWarning("KKL: keine vivenu-Event-Daten erfasst (Rendering nicht verfügbar / Vercel-Block?).");
            return;
        }

        var events = new Dictionary<string, KklImporter.Event>();
        foreach (var j in sammlung.ApiKoerper)
        {
            var ev = KklImporter.Parse(j);
            if (ev != null) events[ev.Id] = ev; // dieselbe Antwort kann mehrfach kommen → dedupe nach ID
        }
        logger.LogInformation("KKL: {N} Events aus {M} API-Antworten, {L} Detail-Links.",
            events.Count, sammlung.ApiKoerper.Count, sammlung.Links.Count);

        int angelegt = 0, gefiltert = 0, uebersprungen = 0;
        foreach (var ev in events.Values)
        {
            ct.ThrowIfCancellationRequested();
            // Dedup über Läufe: bereits entschieden (übernommen/verworfen) → nicht erneut zeigen.
            var bestehend = await db.CrawlFunde.FirstOrDefaultAsync(f => f.ExternKey == ev.Id, ct);
            if (bestehend is { Status: not CrawlFundStatus.Offen }) { uebersprungen++; continue; }

            // Stil-Filter: über die Site-Kategorie bereits erledigt; nur ohne Kategorie via LLM nachfiltern.
            string? llmBand = null;
            if (genre == null)
            {
                var info = await extraktion.KklEventAsync(ev.Name, ev.Beschreibung, _hinweis, ct);
                if (!info.Passt) { gefiltert++; continue; }
                llmBand = info.Band;
            }

            // Detailseite rendern → Programm + Mitwirkende auslesen und strukturieren.
            // Echte KKL-URL aus den Listen-Links bestimmen (vivenu-Slug passt nicht, s. KklImporter.DetailUrl).
            var quellUrl = KklImporter.DetailUrl(ev, sammlung.Links);
            var tabs = await renderer.RenderUndTabsAsync(quellUrl, ["Programm", "Mitwirkende"], ct);
            lauf.SeitenBesucht++;
            var programmText = KklImporter.Abschnitt(tabs.GetValueOrDefault("Programm"),
                "Programm", "Mitwirkende", "Tickets", "Veranstalter", "Event teilen");
            var mitwText = KklImporter.Abschnitt(tabs.GetValueOrDefault("Mitwirkende"),
                "Mitwirkende", "Tickets", "Veranstalter", "Event teilen", "Beschreibung", "Kulinarik");
            var prog = await extraktion.KklProgrammAsync(ev.Name, programmText, mitwText, ct);

            // Bands aus dem Detail (bei Wettbewerben mehrere), sonst der LLM-Stilfilter-Hinweis als Einzelband.
            var bands = prog.Bands.Select(Leer2).Where(b => b != null).Select(b => b!).ToList();
            if (bands.Count == 0 && Leer2(llmBand) is { } fb) bands.Add(fb);
            var einzelBand = bands.Count == 1 ? bands[0] : null; // Stück-/Dirigent-Zuordnung nur bei genau einer Band

            var programm = prog.Stuecke.Count == 0 ? null
                : prog.Stuecke.Select((s, i) => new ProgrammZeileDaten(s.Titel, s.Komponist, einzelBand, i + 1)).ToList();
            // Bands (+ Dirigent:in nur bei genau einer Band) über rangslose „Rang"-Zeilen mitführen
            // (mappt auf KonzertBand + KonzertPerson Dirigent).
            var raenge = bands.Count == 0 ? null
                : bands.Select(b => new RangZeileDaten(b, Dirigent: einzelBand != null ? Leer2(prog.Dirigent) : null)).ToList();

            var daten = new KonzertFundDaten(
                Datum: ev.Datum,
                Uhrzeit: ev.Uhrzeit,
                Name: ev.Name,
                Ort: ev.Saal != null ? $"KKL Luzern, {ev.Saal}" : "KKL Luzern",
                Beschreibung: ev.Beschreibung,
                Programm: programm,
                Raenge: raenge,
                BildUrl: ev.Bild);
            var json = CrawlDaten.Serialisiere(daten);

            if (bestehend != null) // Offen → aktualisieren statt verdoppeln
            {
                bestehend.DatenJson = json;
                bestehend.QuellUrl = quellUrl;
                bestehend.AbgerufenAm = DateTime.UtcNow;
            }
            else
            {
                db.CrawlFunde.Add(new CrawlFund
                {
                    LaufId = lauf.Id,
                    Typ = CrawlFundTyp.Konzert,
                    ExternKey = ev.Id,
                    QuellUrl = quellUrl,
                    AbgerufenAm = DateTime.UtcNow,
                    DatenJson = json,
                    Konfidenz = Konfidenz.Mittel,
                    Status = CrawlFundStatus.Offen
                });
                lauf.FundeAnzahl++;
                angelegt++;
            }
        }
        logger.LogInformation("KKL: {Angelegt} neue Konzert-Funde, {Gefiltert} per Stil-Filter aussortiert, {Skip} schon entschieden.",
            angelegt, gefiltert, uebersprungen);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Legt die CrawlSeite an oder aktualisiert sie. Gibt true zurück, wenn der Inhalt
    /// seit dem letzten Lauf unverändert ist (→ Extraktion überspringbar).</summary>
    private async Task<bool> SeiteMerkenAsync(Guid quelleId, string url, string? hash, bool relevant, CancellationToken ct)
    {
        var seite = await db.CrawlSeiten.FirstOrDefaultAsync(s => s.QuelleId == quelleId && s.Url == url, ct);
        var unveraendert = seite != null && hash != null && seite.InhaltsHash == hash;
        if (seite == null)
        {
            seite = new CrawlSeite { QuelleId = quelleId, Url = url };
            db.CrawlSeiten.Add(seite);
        }
        seite.AbgerufenAm = DateTime.UtcNow;
        seite.InhaltsHash = hash;
        seite.Relevant = relevant;
        return unveraendert;
    }

    /// <summary>Identitäts-Schlüssel + Vollständigkeits-Score eines Funds (für Dedup im Lauf).
    /// Key <c>null</c> → nicht deduplizieren. Höherer Score = mehr gefüllte Felder.</summary>
    private static (string? Key, int Score) Bewerten(ExtrahierterFund f)
    {
        switch (f.Typ)
        {
            case CrawlFundTyp.Leitung:
            {
                var d = CrawlDaten.Deserialisiere<LeitungFundDaten>(f.DatenJson);
                if (d == null || string.IsNullOrWhiteSpace(d.PersonName)) return (null, 0);
                var score = (d.BandName != null ? 1 : 0)
                          + (!string.IsNullOrWhiteSpace(d.Funktion) && d.Funktion != "Dirigent" ? 1 : 0)
                          + (d.VonJahr != null ? 1 : 0) + (d.BisJahr != null ? 1 : 0);
                return ($"L|{Norm(d.PersonName)}|{Norm(d.BandName)}", score);
            }
            case CrawlFundTyp.Konzert:
            {
                var d = CrawlDaten.Deserialisiere<KonzertFundDaten>(f.DatenJson);
                if (d == null) return (null, 0);
                var score = (d.Programm?.Count ?? 0) + (d.Datum != null ? 1 : 0)
                          + (d.Name != null ? 1 : 0) + (d.Ort != null ? 1 : 0);
                return ($"K|{d.Datum}|{Norm(d.Name)}|{Norm(d.Ort)}", score);
            }
            case CrawlFundTyp.Stueck:
            {
                var d = CrawlDaten.Deserialisiere<StueckFundDaten>(f.DatenJson);
                if (d == null || string.IsNullOrWhiteSpace(d.Titel)) return (null, 0);
                var score = (d.KomponistName != null ? 1 : 0) + (d.Jahr != null ? 1 : 0)
                          + (d.Besetzung != null ? 1 : 0) + (d.Beschreibung != null ? 1 : 0);
                return ($"S|{Norm(d.Titel)}", score);
            }
            case CrawlFundTyp.Komponist:
            {
                var d = CrawlDaten.Deserialisiere<KomponistFundDaten>(f.DatenJson);
                if (d == null || string.IsNullOrWhiteSpace(d.Name)) return (null, 0);
                var score = (d.Biografie != null ? 1 : 0) + (d.Geburtsjahr != null ? 1 : 0)
                          + (d.WikipediaUrl != null ? 1 : 0) + (d.BildUrl != null ? 1 : 0);
                return ($"C|{Norm(d.Name)}", score);
            }
            case CrawlFundTyp.Band:
            {
                var d = CrawlDaten.Deserialisiere<BandFundDaten>(f.DatenJson);
                if (d == null || string.IsNullOrWhiteSpace(d.Name)) return (null, 0);
                var score = (d.Land != null ? 1 : 0) + (d.Webseite != null ? 1 : 0)
                          + (d.BildUrl != null ? 1 : 0)
                          + (d.Kategorie != null ? 1 : 0) + (d.Staerkeklasse != null ? 1 : 0)
                          + (d.Gruendungsjahr != null ? 1 : 0) + (d.Geschichte != null ? 1 : 0)
                          + (d.Aliase?.Count ?? 0);
                return ($"B|{Norm(d.Name)}", score);
            }
            default:
                return (null, 0);
        }
    }

    private static string Norm(string? s) => (s ?? "").Trim().ToLowerInvariant();

    /// <summary>Platzhalter-Bandname aus der Domain (z. B. „stadtmusik-luzern.ch" → „Stadtmusik Luzern");
    /// wird beim späteren Vereins-Fund verfeinert/ergänzt.</summary>
    private static string BandNameAusHost(string host)
    {
        var h = host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
        var basis = h.Split('.')[0].Replace('-', ' ').Replace('_', ' ');
        return System.Globalization.CultureInfo.GetCultureInfo("de-CH").TextInfo.ToTitleCase(basis);
    }

    /// <summary>Merkt sich Gremiums-Mitglieder (Leitung-Fund mit Funktion ≠ Dirigent) für die Abgangs-Prüfung.</summary>
    private void BoardMerken(ExtrahierterFund f)
    {
        if (f.Typ != CrawlFundTyp.Leitung) return;
        var d = CrawlDaten.Deserialisiere<LeitungFundDaten>(f.DatenJson);
        if (d == null || string.IsNullOrWhiteSpace(d.PersonName)) return;
        if (string.Equals(d.Funktion, "Dirigent", StringComparison.OrdinalIgnoreCase)) return;
        _boardGesehen.Add($"{Norm(d.PersonName)}|{Norm(d.Funktion)}");
    }

    /// <summary>Erzeugt Hinweis-Funde für aktive Gremiums-Mitgliedschaften der Band, die im aktuellen
    /// Lauf NICHT gefunden wurden (möglicher Abgang). Beendet nichts automatisch – der Admin entscheidet.</summary>
    private async Task AbgaengePruefenAsync(CrawlLauf lauf, Guid bandId, CancellationToken ct)
    {
        var aktiv = await db.BandMitgliedschaften.Include(m => m.Person)
            .Where(m => m.BandId == bandId && m.BisJahr == null
                        && m.Funktion != null && m.Funktion != "Dirigent")
            .ToListAsync(ct);

        foreach (var m in aktiv)
        {
            if (_boardGesehen.Contains($"{Norm(m.Person.Name)}|{Norm(m.Funktion!)}")) continue;
            db.CrawlFunde.Add(new CrawlFund
            {
                LaufId = lauf.Id,
                Typ = CrawlFundTyp.Sonstiges,
                QuellUrl = "",
                AbgerufenAm = DateTime.UtcNow,
                DatenJson = "{}",
                DublettHinweis = $"Beenden prüfen: \"{m.Person.Name}\" – Funktion \"{m.Funktion}\" wurde im "
                    + "aktuellen Crawl nicht mehr gefunden (ggf. im Mitglieder-Editor BisJahr setzen).",
                Status = CrawlFundStatus.Offen
            });
            lauf.FundeAnzahl++;
        }
    }

    /// <summary>Prüft die strukturierten Anforderungen der Quelle gegen einen Fund.
    /// Aktuell: Konzert nur, wenn es mindestens eine Programmzeile hat (KonzertBrauchtStueck).</summary>
    private static bool AnforderungErfuellt(ExtrahierterFund f, CrawlQuelle quelle)
    {
        if (f.Typ == CrawlFundTyp.Konzert && quelle.Anforderungen.HasFlag(CrawlAnforderungen.KonzertBrauchtStueck))
        {
            var d = CrawlDaten.Deserialisiere<KonzertFundDaten>(f.DatenJson);
            if (d?.Programm is not { Count: > 0 }) return false;
        }
        return true;
    }
}
