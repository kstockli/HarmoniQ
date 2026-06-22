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

        try
        {
            if (quelle.Typ == CrawlQuelleTyp.BandDomain)
                await BandDomainCrawlAsync(lauf, quelle, ct);
            else
                await EinzelAbrufAsync(lauf, quelle, quelle.StartUrl, einzelseiteImmerRelevant: true, ct);

            lauf.Status = CrawlLaufStatus.Fertig;
            lauf.Meldung = $"{lauf.SeitenBesucht} Seiten, {lauf.FundeAnzahl} Funde.";
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
        var res = await fetch.HoleAsync(url, ct);
        lauf.SeitenBesucht++;

        if (!res.Erfolg)
        {
            await SeiteMerkenAsync(quelle.Id, url, null, relevant: false, ct);
            await db.SaveChangesAsync(ct);
            logger.LogDebug("Fetch fehlgeschlagen {Url}: {Fehler}", url, res.Fehler);
            return [];
        }

        var text = res.IstPdf ? (res.Text ?? "") : CrawlHtmlHelfer.TextBereinigen(res.Text ?? "");
        var links = res.IstPdf || string.IsNullOrEmpty(res.Text)
            ? new List<string>()
            : CrawlHtmlHelfer.InterneLinks(res.Text!, new Uri(url));

        var relevant = einzelseiteImmerRelevant || SeitenFilter.IstRelevant(url, text);

        var unveraendert = await SeiteMerkenAsync(quelle.Id, url, res.InhaltsHash, relevant, ct);

        if (relevant && !unveraendert && text.Trim().Length > 0)
        {
            var erg = await extraktion.ExtrahiereAsync(
                new ExtraktionsAnfrage(quelle.Typ, url, text, res.IstPdf, _bandName, _hinweis), ct);

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

        await db.SaveChangesAsync(ct);
        return links;
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
            default:
                return (null, 0);
        }
    }

    private static string Norm(string? s) => (s ?? "").Trim().ToLowerInvariant();
}
