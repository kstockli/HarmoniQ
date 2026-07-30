using System.Text;
using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;
using static HarmoniQ.Web.Services.KonzertErfassungService;

namespace HarmoniQ.Web.Services.Emf;

/// <summary>
/// Einmaliger Import der <b>EMF-2026-Parademusik</b> von RTR/SRG „Play". Holt Sections (= Tag + Strasse)
/// und deren Videos über die öffentliche JSON-API (<see cref="EmfImporter"/>) und legt <b>ein Konzert pro
/// Section</b> an: je auftretende Band ein Stück (aus dem Video-Titel) mit dem eingebetteten Video
/// (offizieller SRG-Player, <see cref="VideoPlattform.SrgPlay"/> – kein Download). <b>Dry-run zuerst</b>
/// (<see cref="SammelnAsync"/>/<see cref="VorschauAsync"/> schreiben nichts), dann einzeln/alle importieren.
/// Bands werden per Find-or-create angelegt (nur Name – keine Stammdaten geraten).
/// </summary>
public class EmfImportService(
    HttpClient http,
    IDbContextFactory<ApplicationDbContext> dbf,
    ILogger<EmfImportService> logger)
{
    public record KonzertPlan(DateOnly Datum, string Ort, string Name, string Beschreibung, string Strasse,
        IReadOnlyList<EmfImporter.Video> Videos);

    public record VorschauZeile(DateOnly Datum, string Ort, string Name, int AnzahlBands, int AnzahlVideos,
        int NeueBands, bool KonzertVorhanden, IReadOnlyList<string> Bands);

    public record Vorschau(IReadOnlyList<VorschauZeile> Zeilen, int Konzerte, int Videos, int NeueBands);

    public record Ergebnis(int Konzerte, int BandsNeu, int Videos, IReadOnlyList<string> Fehler);

    /// <summary>Holt alle Sections + deren Videos (parallel) und baut je Section einen Konzert-Plan. Kein DB-Zugriff.</summary>
    public async Task<IReadOnlyList<KonzertPlan>> SammelnAsync(CancellationToken ct = default)
    {
        if (http.DefaultRequestHeaders.UserAgent.Count == 0)
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        var sections = EmfImporter.ParseSections(await http.GetStringAsync(EmfImporter.ShowPageUrl, ct));
        logger.LogInformation("EMF: {N} Sections (Tag+Strasse).", sections.Count);

        var sem = new SemaphoreSlim(4);
        var tasks = sections.Select(async s =>
        {
            await sem.WaitAsync(ct);
            try
            {
                var videos = EmfImporter.ParseVideos(await http.GetStringAsync(EmfImporter.MediaSectionUrl(s.SectionId), ct));
                return (s, videos);
            }
            catch (Exception ex) { logger.LogWarning(ex, "EMF-Section fehlgeschlagen: {Id}", s.SectionId); return (s, (IReadOnlyList<EmfImporter.Video>)[]); }
            finally { sem.Release(); }
        });
        var geladen = await Task.WhenAll(tasks);

        return geladen
            .Where(x => x.Item2.Count > 0)
            .OrderBy(x => x.s.Datum).ThenBy(x => x.s.Strasse)
            .Select(x =>
            {
                var kurz = x.s.Strasse.Split(" - ", 2)[0].Trim();
                return new KonzertPlan(x.s.Datum, $"{kurz}, Biel/Bienne", $"EMF 2026 Parademusik – {kurz}",
                    Beschreibung(x.s), x.s.Strasse, x.Item2);
            })
            .ToList();
    }

    private static string Beschreibung(EmfImporter.Section s) =>
        $"Eidgenössisches Musikfest 2026 · Parademusik · {s.Strasse} · {s.Datum:dd.MM.yyyy}\nVideos: RTR/SRG Play.";

    /// <summary>Dry-run: welche Konzerte/Bands neu wären, ohne zu schreiben.</summary>
    public async Task<Vorschau> VorschauAsync(IReadOnlyList<KonzertPlan> plaene, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var bekannt = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        async Task<bool> BandExistiert(string name)
        {
            if (bekannt.TryGetValue(name, out var v)) return v;
            v = await db.Bands.AnyAsync(b => b.Name == name || b.Aliase.Any(a => a.Name == name), ct);
            return bekannt[name] = v;
        }

        var zeilen = new List<VorschauZeile>();
        foreach (var p in plaene)
        {
            var bands = p.Videos.Where(v => v.Band != null).Select(v => v.Band!).Distinct().ToList();
            var neueBands = 0;
            foreach (var b in bands) if (!await BandExistiert(b)) neueBands++;

            var ids = await db.Konzerte.Where(k => k.Datum == p.Datum && k.Name == p.Name && k.Ort == p.Ort).Select(k => k.Id).ToListAsync(ct);
            var vorhanden = ids.Count > 0 && await db.KonzertBands.AnyAsync(kb => ids.Contains(kb.KonzertId) && bands.Contains(kb.Band.Name), ct);

            zeilen.Add(new VorschauZeile(p.Datum, p.Ort, p.Name, bands.Count, p.Videos.Count(v => v.Band != null), neueBands, vorhanden, bands));
        }
        return new Vorschau(zeilen, plaene.Count, zeilen.Sum(z => z.AnzahlVideos), zeilen.Sum(z => z.NeueBands));
    }

    /// <summary>Importiert ein Konzert (Section): Bands find-or-create, Konzert mit Programm, dann die
    /// eingebetteten Videos (SRG-Player) je Band/Stück verknüpft. Idempotent.</summary>
    public async Task<Ergebnis> ImportiereKonzertAsync(KonzertPlan plan, CancellationToken ct = default)
    {
        var auftritte = plan.Videos.Where(v => v.Band != null).ToList();
        if (auftritte.Count == 0) return new Ergebnis(0, 0, 0, ["(keine Videos mit erkennbarer Band)"]);
        try
        {
            await using var db = await dbf.CreateDbContextAsync(ct);

            // 1) Bands find-or-create (nur Name – Parademusik-Bands sind gemischt, keine Stammdaten raten).
            var bandsNeu = 0;
            foreach (var name in auftritte.Select(a => a.Band!).Distinct())
                if (!await db.Bands.AnyAsync(b => b.Name == name || b.Aliase.Any(al => al.Name == name), ct))
                { db.Bands.Add(new Band { Name = name }); bandsNeu++; }
            await db.SaveChangesAsync(ct);

            // 2) Konzert (Programm = je Auftritt ein Stück, Band gesetzt). Ohne erkanntes Stück → „Parademusik".
            var programm = auftritte
                .Select((a, i) => new ProgrammEingabe(string.IsNullOrWhiteSpace(a.Stueck) ? "Parademusik" : a.Stueck!, null, a.Band, i + 1))
                .ToList();
            var eingabe = new Eingabe(plan.Datum, null, plan.Name, plan.Ort, plan.Beschreibung, null, programm, []);
            var konzertId = await ErfasseOderAktualisiereAsync(db, eingabe);

            // 3) Videos einbetten (SRG-Player), je Auftritt an dessen Stück + Band gehängt.
            await db.SaveChangesAsync(ct);
            var ks = await db.KonzertStuecke.Include(x => x.Stueck).Where(x => x.KonzertId == konzertId).ToListAsync(ct);
            // Band je Titel-Name über Name ODER Alias auflösen (Bands können unter abweichendem Namen
            // existieren, z. B. „Feldmusikmenznau" mit Alias „Feldmusik Menznau" aus dem Vereins-Import).
            var namen = auftritte.Select(a => a.Band!).Distinct().ToList();
            var relevanteBands = await db.Bands.Include(b => b.Aliase)
                .Where(b => namen.Contains(b.Name) || b.Aliase.Any(al => namen.Contains(al.Name))).ToListAsync(ct);
            var bandCache = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in namen)
            {
                var band = relevanteBands.FirstOrDefault(b => b.Name == name || b.Aliase.Any(al => al.Name == name));
                if (band != null) bandCache[name] = band.Id;
            }
            var videos = 0;
            foreach (var a in auftritte)
            {
                if (!bandCache.TryGetValue(a.Band!, out var bandId)) continue;
                var titel = string.IsNullOrWhiteSpace(a.Stueck) ? "Parademusik" : a.Stueck!;
                var stueckId = (ks.FirstOrDefault(x => x.BandId == bandId && x.Stueck.Titel == titel)
                                ?? ks.FirstOrDefault(x => x.Stueck.Titel == titel))?.StueckId;
                if (stueckId is null) continue;
                var vorhandenes = await db.Videos.FirstOrDefaultAsync(
                    x => x.KonzertId == konzertId && x.Plattform == VideoPlattform.SrgPlay && x.ExternId == a.Urn, ct);
                if (vorhandenes is not null)
                {
                    // Bestehendes Video: fehlendes Vorschaubild nachtragen (Backfill bei Re-Import).
                    if (string.IsNullOrWhiteSpace(vorhandenes.BildUrl) && !string.IsNullOrWhiteSpace(a.BildUrl))
                        vorhandenes.BildUrl = a.BildUrl;
                    continue;
                }
                db.Videos.Add(new Video
                {
                    Plattform = VideoPlattform.SrgPlay,
                    ExternId = a.Urn,
                    KonzertId = konzertId,
                    StueckId = stueckId.Value,
                    BandId = bandId,
                    Titel = a.Titel,
                    BildUrl = a.BildUrl,
                    Status = VideoStatus.Genehmigt
                });
                videos++;
            }
            await db.SaveChangesAsync(ct);
            return new Ergebnis(1, bandsNeu, videos, []);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "EMF-Konzert-Import fehlgeschlagen: {Name} {Datum}", plan.Name, plan.Datum);
            return new Ergebnis(0, 0, 0, [$"{plan.Name} ({plan.Datum:dd.MM.yyyy}): {ex.Message}"]);
        }
    }

    public async Task<Ergebnis> ImportierenAsync(IReadOnlyList<KonzertPlan> plaene, CancellationToken ct = default)
    {
        int k = 0, b = 0, v = 0; var fehler = new List<string>();
        foreach (var p in plaene)
        {
            ct.ThrowIfCancellationRequested();
            var r = await ImportiereKonzertAsync(p, ct);
            k += r.Konzerte; b += r.BandsNeu; v += r.Videos; fehler.AddRange(r.Fehler);
        }
        logger.LogInformation("EMF-Import fertig: {K} Konzerte, {B} neue Bands, {V} Videos, {F} Fehler.", k, b, v, fehler.Count);
        return new Ergebnis(k, b, v, fehler);
    }
}
