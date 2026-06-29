using System.Text;
using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;
using static HarmoniQ.Web.Services.KonzertErfassungService;

namespace HarmoniQ.Web.Services.Wmc;

/// <summary>
/// Einmaliger Import des <b>World Music Contest Kerkrade 2026</b>. Holt Liste + Detailseiten (serverseitig
/// gerendert → reines HTTP), parst sie mit <see cref="WmcImporter"/> und legt <b>ein Konzert pro Tag und
/// Veranstaltungsort</b> an (Wettbewerbs-Session mit mehreren Bands, je Band Programm + Dirigent:in).
/// <b>Dry-run zuerst:</b> <see cref="SammelnAsync"/>/<see cref="Gruppiere"/>/<see cref="VorschauAsync"/>
/// schreiben nichts; <see cref="ImportiereKonzertAsync"/> (einzeln) bzw. <see cref="ImportierenAsync"/> (alle)
/// persistieren. Band-Stammdaten (Bio, Kategorie, Stärkeklasse, Land) werden NUR bei neu angelegten Bands
/// gesetzt – bestehende, kuratierte Bands bleiben unangetastet.
/// </summary>
public class WmcImportService(
    HttpClient http,
    IDbContextFactory<ApplicationDbContext> dbf,
    ILogger<WmcImportService> logger)
{
    /// <summary>Ein geplantes Konzert = eine Session (Tag + Ort) mit allen dort auftretenden Bands.</summary>
    public record KonzertPlan(DateOnly Datum, string Ort, string Name, string Beschreibung,
        IReadOnlyList<WmcImporter.Auftritt> Auftritte);

    public record VorschauZeile(DateOnly Datum, string Ort, string Name, int AnzahlBands, int AnzahlStuecke,
        int NeueBands, bool KonzertVorhanden, IReadOnlyList<string> Bands);

    public record Vorschau(IReadOnlyList<VorschauZeile> Zeilen, int Konzerte, int Auftritte, int NeueBands,
        int OhneDatum, IReadOnlyList<string> AuftritteOhneDatum);

    public record Ergebnis(int Konzerte, int BandsNeu, int StueckeGesamt, IReadOnlyList<string> Fehler);

    /// <summary>Holt Liste + alle Teilnehmer-Detailseiten (parallel, gedrosselt) und parst sie. Kein DB-Zugriff.</summary>
    public async Task<IReadOnlyList<WmcImporter.Auftritt>> SammelnAsync(CancellationToken ct = default)
    {
        if (http.DefaultRequestHeaders.UserAgent.Count == 0)
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        var listHtml = await http.GetStringAsync(WmcImporter.ListenUrl, ct);
        var zeilen = WmcImporter.ParseListe(listHtml);
        logger.LogInformation("WMC: {N} Teilnehmer-Zeilen in der Liste.", zeilen.Count);

        var sem = new SemaphoreSlim(6);
        var tasks = zeilen.Select(async z =>
        {
            await sem.WaitAsync(ct);
            try { return WmcImporter.ParseDetail(await http.GetStringAsync(WmcImporter.BasisUrl + z.Href, ct), z); }
            catch (Exception ex) { logger.LogWarning(ex, "WMC-Detail fehlgeschlagen: {Href}", z.Href); return null; }
            finally { sem.Release(); }
        });
        var alle = (await Task.WhenAll(tasks)).Where(a => a != null).Select(a => a!).ToList();
        var auftritte = alle.Where(a => a.Kategorie != null).ToList(); // nur die vier Concert-Contest-Kategorien
        logger.LogInformation("WMC: {Auf} Auftritte ({Aus} ohne erkannte Kategorie übersprungen).",
            auftritte.Count, alle.Count - auftritte.Count);
        return auftritte;
    }

    /// <summary>Gruppiert die Auftritte zu Konzerten: <b>ein Konzert pro (Datum, Ort)</b>. Auftritte ohne
    /// erkennbares Datum können keinem Konzert zugeordnet werden und entfallen (im Dry-run ausgewiesen).</summary>
    public IReadOnlyList<KonzertPlan> Gruppiere(IReadOnlyList<WmcImporter.Auftritt> auftritte)
    {
        return auftritte
            .Where(a => a.Datum is not null && !string.IsNullOrWhiteSpace(a.Ort))
            .GroupBy(a => (Datum: a.Datum!.Value, Ort: a.Ort!))
            .OrderBy(g => g.Key.Datum).ThenBy(g => g.Key.Ort)
            .Select(g =>
            {
                var list = g.OrderBy(a => a.Zeit ?? "~").ToList();
                return new KonzertPlan(g.Key.Datum, g.Key.Ort, KonzertName(list), KonzertBeschreibung(g.Key.Datum, g.Key.Ort, list), list);
            })
            .ToList();
    }

    private static string KonzertName(IReadOnlyList<WmcImporter.Auftritt> list)
    {
        var labels = list
            .Select(a => string.Join(" ", new[] { a.Kategorie, a.DivisionLabel }.Where(x => !string.IsNullOrWhiteSpace(x))))
            .Where(s => s.Length > 0).Distinct().ToList();
        return labels.Count > 0 ? $"WMC {WmcImporter.Jahr} – {string.Join(" / ", labels)}" : $"WMC {WmcImporter.Jahr}";
    }

    private static string KonzertBeschreibung(DateOnly datum, string ort, IReadOnlyList<WmcImporter.Auftritt> list)
    {
        var sb = new StringBuilder($"World Music Contest Kerkrade {WmcImporter.Jahr} · {ort} · {datum:dd.MM.yyyy}");
        sb.Append("\nStartreihenfolge:");
        foreach (var a in list)
            sb.Append('\n').Append(a.Zeit is null ? "" : a.Zeit + " ").Append(a.BandName)
              .Append(a.Land is null ? "" : $" ({a.Land})");
        var sol = list.Where(a => a.Solisten.Count > 0).Select(a => $"{a.BandName}: {string.Join(", ", a.Solisten)}").ToList();
        if (sol.Count > 0) sb.Append("\nSolist:innen – ").Append(string.Join(" · ", sol));
        return sb.ToString();
    }

    /// <summary>Dry-run: welche Konzerte/Bands neu wären, ohne zu schreiben.</summary>
    public async Task<Vorschau> VorschauAsync(IReadOnlyList<KonzertPlan> plaene,
        IReadOnlyList<WmcImporter.Auftritt> alle, CancellationToken ct = default)
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
            var bands = p.Auftritte.Select(a => a.BandName).Distinct().ToList();
            var neueBands = 0;
            foreach (var b in bands) if (!await BandExistiert(b)) neueBands++;

            var ids = await db.Konzerte.Where(k => k.Datum == p.Datum && k.Name == p.Name && k.Ort == p.Ort).Select(k => k.Id).ToListAsync(ct);
            var vorhanden = ids.Count > 0 && await db.KonzertBands.AnyAsync(kb => ids.Contains(kb.KonzertId) && bands.Contains(kb.Band.Name), ct);

            zeilen.Add(new VorschauZeile(p.Datum, p.Ort, p.Name, bands.Count,
                p.Auftritte.Sum(a => a.Stuecke.Count), neueBands, vorhanden, bands));
        }

        var ohneDatum = alle.Where(a => a.Datum is null || string.IsNullOrWhiteSpace(a.Ort)).Select(a => a.BandName).ToList();
        return new Vorschau(zeilen, plaene.Count, alle.Count, zeilen.Sum(z => z.NeueBands), ohneDatum.Count, ohneDatum);
    }

    /// <summary>Importiert ein einzelnes Konzert (Session): Bands find-or-create (Stammdaten nur bei neuen)
    /// und das Konzert mit Programm + Dirigent:innen. Idempotent.</summary>
    public async Task<Ergebnis> ImportiereKonzertAsync(KonzertPlan plan, CancellationToken ct = default)
    {
        var fehler = new List<string>();
        try
        {
            await using var db = await dbf.CreateDbContextAsync(ct);

            // 1) Bands: find-or-create; Stammdaten (Bio/Kategorie/Stärkeklasse/Land) NUR bei neu angelegten.
            var bandsNeu = 0;
            foreach (var a in plan.Auftritte.GroupBy(x => x.BandName).Select(g => g.First()))
            {
                var band = await db.Bands.Include(b => b.Aliase)
                    .FirstOrDefaultAsync(b => b.Name == a.BandName || b.Aliase.Any(al => al.Name == a.BandName), ct);
                if (band == null)
                {
                    band = new Band
                    {
                        Name = a.BandName,
                        Land = LandName(a.Land),
                        Geschichte = a.Bio,
                        Kategorie = a.KategorieEnum,
                        Staerkeklasse = a.Staerke
                    };
                    db.Bands.Add(band);
                    bandsNeu++;
                }
            }
            await db.SaveChangesAsync(ct); // Bands sichtbar machen, bevor der Konzert-Service sie sucht

            // 2) Konzert: Programm (alle Bands) + Dirigent:innen.
            var programm = plan.Auftritte
                .SelectMany(a => a.Stuecke.Where(s => !string.IsNullOrWhiteSpace(s.Titel))
                    .Select(s => new ProgrammEingabe(s.Titel, s.Komponist, a.BandName, null)))
                .Select((pe, i) => pe with { Reihenfolge = i + 1 })
                .ToList();
            var mitwirkende = plan.Auftritte
                .Where(a => !string.IsNullOrWhiteSpace(a.Dirigent))
                .Select(a => new MitwirkendeEingabe(a.Dirigent!, PersonRolleTyp.Dirigent, a.BandName))
                .ToList();

            var eingabe = new Eingabe(plan.Datum, plan.Name, plan.Ort, plan.Beschreibung, null, programm, mitwirkende);
            await ErfasseOderAktualisiereAsync(db, eingabe);

            return new Ergebnis(1, bandsNeu, programm.Count, fehler);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "WMC-Konzert-Import fehlgeschlagen: {Name} {Datum}", plan.Name, plan.Datum);
            fehler.Add($"{plan.Name} ({plan.Datum:dd.MM.yyyy}): {ex.Message}");
            return new Ergebnis(0, 0, 0, fehler);
        }
    }

    /// <summary>Importiert alle geplanten Konzerte nacheinander.</summary>
    public async Task<Ergebnis> ImportierenAsync(IReadOnlyList<KonzertPlan> plaene, CancellationToken ct = default)
    {
        int konzerte = 0, bandsNeu = 0, stuecke = 0;
        var fehler = new List<string>();
        foreach (var p in plaene)
        {
            ct.ThrowIfCancellationRequested();
            var r = await ImportiereKonzertAsync(p, ct);
            konzerte += r.Konzerte; bandsNeu += r.BandsNeu; stuecke += r.StueckeGesamt; fehler.AddRange(r.Fehler);
        }
        logger.LogInformation("WMC-Import fertig: {K} Konzerte, {B} neue Bands, {S} Stücke, {F} Fehler.",
            konzerte, bandsNeu, stuecke, fehler.Count);
        return new Ergebnis(konzerte, bandsNeu, stuecke, fehler);
    }

    private static string? LandName(string? code) => code?.ToUpperInvariant() switch
    {
        null or "" => null,
        "CH" => "Schweiz", "DE" => "Deutschland", "AT" => "Österreich", "BE" => "Belgien",
        "NL" => "Niederlande", "FR" => "Frankreich", "IT" => "Italien", "GB" or "UK" => "Grossbritannien",
        "NO" => "Norwegen", "SE" => "Schweden", "DK" => "Dänemark", "FI" => "Finnland",
        "ES" => "Spanien", "PT" => "Portugal", "LT" or "LTU" => "Litauen", "LU" => "Luxemburg",
        "SI" => "Slowenien", "JP" => "Japan", "US" or "USA" => "USA", "AU" => "Australien",
        _ => code
    };
}
