using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services.Kmvw;

/// <summary>
/// Einmaliger Import/Anreicherung der Walliser Blasmusikvereine von kmvw.ch (Kantonaler Musikverband Wallis).
/// Reichert <b>bestehende</b> Bands an (nur leere Felder: Logo, Homepage, E-Mail, Facebook) und legt <b>fehlende</b>
/// neu an. Legt zu Dirigent:in und Präsident:in je eine <see cref="Person"/> + <see cref="BandMitgliedschaft"/>
/// (Funktion, mit E-Mail sofern vorhanden) an. Für Vereine mit Homepage entsteht – analog LKBV – ein
/// <see cref="CrawlQuelle"/>-Eintrag (BandDomain), damit der Crawler künftig Konzerte findet. Abgleich
/// order-/diakritik-unabhängig über <see cref="KmvwImporter.WortSchluessel"/> (Name „&lt;Name&gt; &lt;Ort&gt;" + Aliase).
/// Dry-run zuerst (<see cref="SammelnAsync"/>/<see cref="VorschauAsync"/> schreiben nichts);
/// <see cref="ImportierenAsync"/> persistiert.
/// </summary>
public class KmvwImportService(
    HttpClient http,
    IDbContextFactory<ApplicationDbContext> dbf,
    ILogger<KmvwImportService> logger)
{
    /// <summary>Ein Verein wie auf der Seite (Name = „&lt;h2&gt; &lt;Ort&gt;" für Eindeutigkeit, Ort-Duplikate).</summary>
    public record Verein(string Name, string? Ort, string? LogoUrl, string? EMail, string? Webseite, string? Facebook,
        KmvwImporter.Funktionaer? Praesident, KmvwImporter.Funktionaer? Dirigent);

    public record VorschauZeile(string Name, string? MatchName, bool Neu, IReadOnlyList<string> Felder,
        IReadOnlyList<string> Personen, bool CrawlerNeu);
    public record Vorschau(IReadOnlyList<VorschauZeile> Zeilen, int Total, int Neu, int Anzureichern,
        int PersonenNeu, int CrawlerEintraege);
    public record Ergebnis(int Neu, int Angereichert, int Uebersprungen, int PersonenNeu, int MitgliedschaftenNeu,
        int CrawlerNeu, IReadOnlyList<string> Fehler);

    /// <summary>Holt die eine Seite und parst sie zu Vereinen. Kein DB-Zugriff.</summary>
    public async Task<IReadOnlyList<Verein>> SammelnAsync(CancellationToken ct = default)
    {
        if (http.DefaultRequestHeaders.UserAgent.Count == 0)
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        var roh = KmvwImporter.ParseSeite(await http.GetStringAsync(KmvwImporter.SeiteUrl, ct));
        logger.LogInformation("KMVW: {N} Vereine geparst.", roh.Count);
        return roh.Select(r => new Verein(
            BandName(r.Name, r.Ort), r.Ort, r.LogoUrl, r.EMail, r.Webseite, r.Facebook, r.Praesident, r.Dirigent)).ToList();
    }

    /// <summary>Band-Name = „&lt;Name&gt; &lt;Ort&gt;", da die kurzen Vereinsnamen (ABEILLE, ALPENGRUSS …) sich sonst
    /// über verschiedene Orte überschneiden würden.</summary>
    private static string BandName(string name, string? ort) =>
        string.IsNullOrWhiteSpace(ort) ? name : $"{name} {ort}";

    private static Dictionary<string, (Guid Id, string Name)> IndexBauen(IEnumerable<Band> bands)
    {
        var index = new Dictionary<string, (Guid, string)>(StringComparer.Ordinal);
        foreach (var b in bands)
        {
            void Add(string n) { var k = KmvwImporter.WortSchluessel(n); if (k.Length > 0 && !index.ContainsKey(k)) index[k] = (b.Id, b.Name); }
            Add(b.Name);
            foreach (var a in b.Aliase) Add(a.Name);
        }
        return index;
    }

    /// <summary>Dry-run: pro Verein neu/anreichern, welche Felder + Personen gesetzt würden, ob ein Crawler-Eintrag entsteht.</summary>
    public async Task<Vorschau> VorschauAsync(IReadOnlyList<Verein> vereine, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var bands = await db.Bands.Include(b => b.Aliase).Include(b => b.Links)
            .Include(b => b.Mitgliedschaften).ThenInclude(m => m.Person).ToListAsync(ct);
        var index = IndexBauen(bands);
        var byId = bands.ToDictionary(b => b.Id);
        var domains = await ExistierendeDomainsAsync(db, ct);

        var zeilen = new List<VorschauZeile>();
        int personenNeu = 0;
        var gesehenePersonen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in await db.Personen.Select(p => p.Name).ToListAsync(ct)) gesehenePersonen.Add(p);

        foreach (var v in vereine)
        {
            Band? band = null; string? matchName = null;
            if (index.TryGetValue(KmvwImporter.WortSchluessel(v.Name), out var m) && byId.TryGetValue(m.Id, out var b))
            { band = b; matchName = m.Name; }

            var neu = band is null;
            var felder = neu ? new List<string> { "neu (Logo/Homepage/E-Mail/Facebook)" } : WuerdeSetzen(band!, v).ToList();

            var personen = new List<string>();
            foreach (var (f, funktion) in Funktionaere(v))
            {
                var hatBereits = band?.Mitgliedschaften.Any(x => string.Equals(x.Funktion, funktion, StringComparison.OrdinalIgnoreCase)) == true;
                if (hatBereits) continue;
                personen.Add($"{funktion}: {f.Name}");
                if (gesehenePersonen.Add(f.Name)) personenNeu++;
            }

            var homepage = !string.IsNullOrWhiteSpace(band?.Webseite) ? band!.Webseite : v.Webseite;
            var kern = DomainKern(homepage);
            var crawlerNeu = kern != null && domains.Add(kern);
            zeilen.Add(new VorschauZeile(v.Name, matchName, neu, felder, personen, crawlerNeu));
        }
        return new Vorschau(zeilen, vereine.Count, zeilen.Count(z => z.Neu),
            zeilen.Count(z => !z.Neu && z.Felder.Count > 0), personenNeu, zeilen.Count(z => z.CrawlerNeu));
    }

    /// <summary>Schreibt: bestehende Bands anreichern (nur leere Felder), fehlende neu anlegen, Dirigent/Präsident +
    /// Homepage-Crawler-Eintrag.</summary>
    public async Task<Ergebnis> ImportierenAsync(IReadOnlyList<Verein> vereine, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var bands = await db.Bands.Include(b => b.Aliase).Include(b => b.Links)
            .Include(b => b.Mitgliedschaften).ThenInclude(m => m.Person).ToListAsync(ct);
        var index = IndexBauen(bands);
        var byId = bands.ToDictionary(b => b.Id);
        var domains = await ExistierendeDomainsAsync(db, ct);

        var personen = await db.Personen.Include(p => p.Rollen).Include(p => p.Links).ToListAsync(ct);
        var personByName = new Dictionary<string, Person>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in personen) personByName.TryAdd(p.Name, p);

        int neu = 0, angereichert = 0, skip = 0, personenNeu = 0, mitglNeu = 0, crawlerNeu = 0;
        var fehler = new List<string>();
        foreach (var v in vereine)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                Band band;
                if (index.TryGetValue(KmvwImporter.WortSchluessel(v.Name), out var m) && byId.TryGetValue(m.Id, out var vorhanden))
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

                // Dirigent:in / Präsident:in → Person + Mitgliedschaft (keine Dublette gleicher Funktion je Band).
                foreach (var (f, funktion) in Funktionaere(v))
                {
                    if (band.Mitgliedschaften.Any(x => string.Equals(x.Funktion, funktion, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    if (!personByName.TryGetValue(f.Name, out var person))
                    {
                        person = new Person { Name = f.Name, Sichtbarkeit = Sichtbarkeit.Oeffentlich };
                        db.Personen.Add(person);
                        personByName[f.Name] = person;
                        personenNeu++;
                    }
                    person.Sichtbarkeit = Sichtbarkeit.Oeffentlich;
                    var rolle = string.Equals(funktion, "Dirigent", StringComparison.OrdinalIgnoreCase)
                        ? PersonRolleTyp.Dirigent : PersonRolleTyp.Musikant;
                    if (person.Rollen.All(r => r.Rolle != rolle)) person.Rollen.Add(new PersonRolle { Rolle = rolle });
                    if (!string.IsNullOrWhiteSpace(f.EMail) && string.IsNullOrWhiteSpace(person.EMail)) person.EMail = f.EMail!.Trim();

                    var mitgl = new BandMitgliedschaft { Band = band, Person = person, Funktion = funktion };
                    band.Mitgliedschaften.Add(mitgl);
                    db.BandMitgliedschaften.Add(mitgl);
                    mitglNeu++;
                }

                // BandDomain-Crawler-Eintrag für die Homepage, falls die Domain noch keinen hat.
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
            catch (Exception ex) { fehler.Add($"{v.Name}: {ex.Message}"); logger.LogWarning(ex, "KMVW-Import {Name}", v.Name); }
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("KMVW-Import: {Neu} neu, {Ang} angereichert, {Skip} unverändert, {P} Personen, {Mi} Mitgliedschaften, {Cr} Crawler, {F} Fehler.",
            neu, angereichert, skip, personenNeu, mitglNeu, crawlerNeu, fehler.Count);
        return new Ergebnis(neu, angereichert, skip, personenNeu, mitglNeu, crawlerNeu, fehler);
    }

    /// <summary>Dirigent:in (Funktion „Dirigent") + Präsident:in (Funktion „Präsident"), soweit vorhanden.</summary>
    private static IEnumerable<(KmvwImporter.Funktionaer F, string Funktion)> Funktionaere(Verein v)
    {
        if (v.Dirigent is { } d) yield return (d, "Dirigent");
        if (v.Praesident is { } p) yield return (p, "Präsident");
    }

    private static async Task<HashSet<string>> ExistierendeDomainsAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var roh = await db.CrawlQuellen.Where(q => q.Domain != null).Select(q => q.Domain!).ToListAsync(ct);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in roh) { var k = Kern(d); if (k != null) set.Add(k); }
        return set;
    }

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

    private static IReadOnlyList<string> WuerdeSetzen(Band b, Verein v)
    {
        var f = new List<string>();
        if (string.IsNullOrWhiteSpace(b.BildUrl) && !string.IsNullOrWhiteSpace(v.LogoUrl)) f.Add("Logo");
        if (string.IsNullOrWhiteSpace(b.Webseite) && !string.IsNullOrWhiteSpace(v.Webseite)) f.Add("Homepage");
        if (string.IsNullOrWhiteSpace(b.EMail) && !string.IsNullOrWhiteSpace(v.EMail)) f.Add("E-Mail");
        if (string.IsNullOrWhiteSpace(b.Facebook) && !string.IsNullOrWhiteSpace(v.Facebook)) f.Add("Facebook");
        return f;
    }

    /// <summary>Setzt nur leere Felder (kuratierte Daten nicht überschreiben). true, wenn etwas gesetzt wurde.</summary>
    private static bool Setzen(Band b, Verein v)
    {
        var g = false;
        if (string.IsNullOrWhiteSpace(b.BildUrl) && !string.IsNullOrWhiteSpace(v.LogoUrl)) { b.BildUrl = v.LogoUrl; g = true; }
        if (string.IsNullOrWhiteSpace(b.Webseite) && !string.IsNullOrWhiteSpace(v.Webseite)) { b.Webseite = v.Webseite; g = true; }
        if (string.IsNullOrWhiteSpace(b.EMail) && !string.IsNullOrWhiteSpace(v.EMail)) { b.EMail = v.EMail; g = true; }
        if (string.IsNullOrWhiteSpace(b.Facebook) && !string.IsNullOrWhiteSpace(v.Facebook)) { b.Facebook = v.Facebook; g = true; }
        return g;
    }
}
