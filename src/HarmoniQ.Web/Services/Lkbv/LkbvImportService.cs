using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services.Lkbv;

/// <summary>
/// Einmaliger Import/Anreicherung der Luzerner Blasmusikvereine von lkbv.ch. Reichert <b>bestehende</b> Bands
/// an (nur leere Felder: Foto, Homepage, Gründungsjahr, Kategorie, Stärkeklasse) und legt <b>fehlende</b> neu an.
/// Abgleich order-/diakritik-unabhängig über <see cref="LkbvImporter.WortSchluessel"/> (Name + Aliase).
/// Fotos werden nur verlinkt (nicht gehostet) mit Quellenangabe. Dry-run zuerst (<see cref="SammelnAsync"/>/
/// <see cref="VorschauAsync"/> schreiben nichts); <see cref="ImportierenAsync"/> persistiert.
/// </summary>
public class LkbvImportService(
    HttpClient http,
    IDbContextFactory<ApplicationDbContext> dbf,
    ILogger<LkbvImportService> logger)
{
    public record Verein(string Name, string? FotoUrl, int? Gruendungsjahr, Staerkeklasse? Klasse,
        BandKategorie? Kategorie, string? Webseite, string DetailUrl);

    public record VorschauZeile(string Name, string? MatchName, bool Neu, IReadOnlyList<string> Felder, bool CrawlerNeu);
    public record Vorschau(IReadOnlyList<VorschauZeile> Zeilen, int Total, int Neu, int Anzureichern, int CrawlerEintraege);
    public record Ergebnis(int Neu, int Angereichert, int Uebersprungen, int CrawlerNeu, IReadOnlyList<string> Fehler);

    /// <summary>Holt Galerie + alle Detailseiten (parallel, gedrosselt) und parst sie. Kein DB-Zugriff.</summary>
    public async Task<IReadOnlyList<Verein>> SammelnAsync(CancellationToken ct = default)
    {
        if (http.DefaultRequestHeaders.UserAgent.Count == 0)
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        var eintraege = LkbvImporter.ParseGalerie(await http.GetStringAsync(LkbvImporter.GalerieUrl, ct));
        logger.LogInformation("LKBV: {N} Vereine in der Galerie.", eintraege.Count);

        var sem = new SemaphoreSlim(6);
        var tasks = eintraege.Select(async e =>
        {
            await sem.WaitAsync(ct);
            try
            {
                var d = LkbvImporter.ParseDetail(await http.GetStringAsync(e.DetailUrl, ct));
                return new Verein(e.Name, e.FotoUrl, d.Gruendungsjahr, d.Klasse, d.Kategorie, d.Webseite, e.DetailUrl);
            }
            catch (Exception ex) { logger.LogWarning(ex, "LKBV-Detail fehlgeschlagen: {Url}", e.DetailUrl);
                return new Verein(e.Name, e.FotoUrl, null, null, null, null, e.DetailUrl); }
            finally { sem.Release(); }
        });
        return (await Task.WhenAll(tasks)).ToList();
    }

    /// <summary>Baut den order-unabhängigen Namensindex (Wortschlüssel → Band-Id) aus Namen + Aliasen.</summary>
    private static Dictionary<string, (Guid Id, string Name)> IndexBauen(IEnumerable<Band> bands)
    {
        var index = new Dictionary<string, (Guid, string)>(StringComparer.Ordinal);
        foreach (var b in bands)
        {
            void Add(string n) { var k = LkbvImporter.WortSchluessel(n); if (k.Length > 0 && !index.ContainsKey(k)) index[k] = (b.Id, b.Name); }
            Add(b.Name);
            foreach (var a in b.Aliase) Add(a.Name);
        }
        return index;
    }

    /// <summary>Dry-run: pro Verein neu/anreichern + welche Felder gesetzt würden (nur leere).</summary>
    public async Task<Vorschau> VorschauAsync(IReadOnlyList<Verein> vereine, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var bands = await db.Bands.Include(b => b.Aliase).ToListAsync(ct);
        var index = IndexBauen(bands);
        var byId = bands.ToDictionary(b => b.Id);
        var domains = await ExistierendeDomainsAsync(db, ct);

        var zeilen = new List<VorschauZeile>();
        foreach (var v in vereine)
        {
            Band? band = null; string? matchName = null;
            if (index.TryGetValue(LkbvImporter.WortSchluessel(v.Name), out var m) && byId.TryGetValue(m.Id, out var b))
            { band = b; matchName = m.Name; }

            var neu = band is null;
            var felder = neu ? new List<string> { "neu (alle Felder)" } : WuerdeSetzen(band!, v).ToList();
            var homepage = !string.IsNullOrWhiteSpace(band?.Webseite) ? band!.Webseite : v.Webseite;
            var kern = DomainKern(homepage);
            var crawlerNeu = kern != null && domains.Add(kern); // Add == true → noch nicht vorhanden (dedupt auch innerhalb)
            zeilen.Add(new VorschauZeile(v.Name, matchName, neu, felder, crawlerNeu));
        }
        return new Vorschau(zeilen, vereine.Count, zeilen.Count(z => z.Neu),
            zeilen.Count(z => !z.Neu && z.Felder.Count > 0), zeilen.Count(z => z.CrawlerNeu));
    }

    /// <summary>Schreibt: bestehende Bands anreichern (nur leere Felder), fehlende neu anlegen.</summary>
    public async Task<Ergebnis> ImportierenAsync(IReadOnlyList<Verein> vereine, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var bands = await db.Bands.Include(b => b.Aliase).ToListAsync(ct);
        var index = IndexBauen(bands);
        var byId = bands.ToDictionary(b => b.Id);
        var domains = await ExistierendeDomainsAsync(db, ct);

        int neu = 0, angereichert = 0, skip = 0, crawlerNeu = 0;
        var fehler = new List<string>();
        foreach (var v in vereine)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                Band band;
                if (index.TryGetValue(LkbvImporter.WortSchluessel(v.Name), out var m) && byId.TryGetValue(m.Id, out var vorhanden))
                {
                    band = vorhanden;
                    if (Setzen(band, v)) angereichert++; else skip++;
                }
                else
                {
                    band = new Band { Name = v.Name };
                    Setzen(band, v);
                    db.Bands.Add(band);
                    neu++;
                }

                // BandDomain-Crawler-Eintrag für die Homepage anlegen, falls für die Domain noch keiner existiert.
                if (Uri.TryCreate(band.Webseite, UriKind.Absolute, out var u) && DomainKern(band.Webseite) is { } kern && domains.Add(kern))
                {
                    db.CrawlQuellen.Add(new CrawlQuelle
                    {
                        Typ = CrawlQuelleTyp.BandDomain,
                        StartUrl = $"{u.Scheme}://{u.Host}/",
                        Domain = kern,
                        BandId = band.Id,
                        Aktiv = true
                    });
                    crawlerNeu++;
                }
            }
            catch (Exception ex) { fehler.Add($"{v.Name}: {ex.Message}"); logger.LogWarning(ex, "LKBV-Import {Name}", v.Name); }
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("LKBV-Import: {Neu} neu, {Ang} angereichert, {Skip} unverändert, {Cr} Crawler-Einträge, {F} Fehler.",
            neu, angereichert, skip, crawlerNeu, fehler.Count);
        return new Ergebnis(neu, angereichert, skip, crawlerNeu, fehler);
    }

    /// <summary>Menge bereits vorhandener Crawler-Quellen-Domains (normalisiert, ohne „www"), für die Dedup.</summary>
    private static async Task<HashSet<string>> ExistierendeDomainsAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var roh = await db.CrawlQuellen.Where(q => q.Domain != null).Select(q => q.Domain!).ToListAsync(ct);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in roh) { var k = Kern(d); if (k != null) set.Add(k); }
        return set;
    }

    /// <summary>Registrierbarer Host aus einer URL/Domain (klein, ohne „www."). Null, wenn unbrauchbar.</summary>
    private static string? DomainKern(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var host = Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : url;
        return Kern(host);
    }

    private static string? Kern(string? host)
    {
        var h = host?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(h)) return null;
        if (h.StartsWith("www.")) h = h[4..];
        return h.Contains('.') ? h : null;
    }

    /// <summary>Welche (leeren) Felder würden gesetzt – für die Vorschau.</summary>
    private static IReadOnlyList<string> WuerdeSetzen(Band b, Verein v)
    {
        var f = new List<string>();
        if (string.IsNullOrWhiteSpace(b.FotoUrl) && !string.IsNullOrWhiteSpace(v.FotoUrl)) f.Add("Foto");
        if (string.IsNullOrWhiteSpace(b.Webseite) && !string.IsNullOrWhiteSpace(v.Webseite)) f.Add("Homepage");
        if (b.Gruendungsjahr is null && v.Gruendungsjahr is not null) f.Add("Gründung");
        if (b.Kategorie is null && v.Kategorie is not null) f.Add("Kategorie");
        if (b.Staerkeklasse is null && v.Klasse is not null) f.Add("Klasse");
        return f;
    }

    /// <summary>Setzt nur leere Felder (kuratierte Daten nicht überschreiben). Gibt true, wenn etwas gesetzt wurde.</summary>
    private static bool Setzen(Band b, Verein v)
    {
        var g = false;
        if (string.IsNullOrWhiteSpace(b.FotoUrl) && !string.IsNullOrWhiteSpace(v.FotoUrl))
        { b.FotoUrl = v.FotoUrl; b.FotoAttribution ??= LkbvImporter.FotoQuelle; g = true; }
        if (string.IsNullOrWhiteSpace(b.Webseite) && !string.IsNullOrWhiteSpace(v.Webseite)) { b.Webseite = v.Webseite; g = true; }
        if (b.Gruendungsjahr is null && v.Gruendungsjahr is not null) { b.Gruendungsjahr = v.Gruendungsjahr; g = true; }
        if (b.Kategorie is null && v.Kategorie is not null) { b.Kategorie = v.Kategorie; g = true; }
        if (b.Staerkeklasse is null && v.Klasse is not null) { b.Staerkeklasse = v.Klasse; g = true; }
        return g;
    }
}
