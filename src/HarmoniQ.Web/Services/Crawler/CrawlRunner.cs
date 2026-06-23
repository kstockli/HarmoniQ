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
    ILogger<CrawlRunner> logger)
{
    private string? _bandName;
    private string? _hinweis;
    // Dedup innerhalb eines Laufs: je Fund-Identität der bisher vollständigste Datensatz.
    private readonly Dictionary<string, (CrawlFund Fund, int Score)> _gesehen = new();

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
        await db.SaveChangesAsync(ct);

        // Kontext für die Extraktion: Quell-Band (für BandDomain-Zuordnung) + Admin-Hinweis.
        _hinweis = quelle.ExtraktionsHinweis;
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
            else
                await EinzelAbrufAsync(lauf, quelle, quelle.StartUrl, einzelseiteImmerRelevant: true, ct);

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
                new ExtraktionsAnfrage(quelle.Typ, url, text, res.IstPdf, _bandName, _hinweis, logo), ct);

            foreach (var f in erg.Funde)
            {
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
        var externe = CrawlHtmlHelfer.ExterneLinks(html, new Uri(url));
        if (externe.Count > maxVorschlaege)
            logger.LogWarning("Event {Url}: {Total} fremde Domains, max. {Max} werden als Fund angelegt.",
                url, externe.Count, maxVorschlaege);
        externe = externe.Take(maxVorschlaege).ToList();

        // Domains, die der Crawler schon als Quelle kennt, überspringen.
        var bekannt = (await db.CrawlQuellen.Where(q => q.Domain != null).Select(q => q.Domain!).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ziele = externe.Where(l => !bekannt.Contains(new Uri(l).Host)).ToList();
        logger.LogInformation("Vereins-Link-Ernte {Url}: {Gesamt} fremde Domains, {Bekannt} bereits als Quelle " +
            "(übersprungen), {Neu} neu → lade Vorschauen …", url, externe.Count, externe.Count - ziele.Count, ziele.Count);
        if (ziele.Count == 0)
        {
            logger.LogInformation("Keine neuen Domains. Tipp: alte Quellen-Vorschläge löschen, dann werden sie " +
                "als Funde neu aufbereitet.");
            return;
        }

        // Vorschau parallel laden (verschiedene Domains; DB-frei – EF ist nicht thread-safe).
        var drossel = new SemaphoreSlim(8);
        var previews = await Task.WhenAll(ziele.Select(async link =>
        {
            await drossel.WaitAsync(ct);
            try
            {
                var host = new Uri(link).Host;
                var r = await fetch.HoleAsync(link, false, ct);
                string? titel = null, besch = null;
                if (r.Erfolg && !string.IsNullOrEmpty(r.Text))
                    (titel, besch) = CrawlHtmlHelfer.SeitenInfo(r.Text!);
                return new WebseiteFundDaten(link, titel ?? host, titel, besch);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { return new WebseiteFundDaten(link, new Uri(link).Host); }
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
        logger.LogInformation("{Count} Webseiten-Funde (Vereins-Vorschläge) aus {Url} angelegt.", previews.Length, url);
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
}
