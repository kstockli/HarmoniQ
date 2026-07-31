using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Services.Crawler;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Einmalige Bereinigung importierter Fremdtexte (Urheberrecht): schreibt Band-Bios (WMC, meist englisch)
/// und KKL-Konzert-Beschreibungen per LLM in eigenen deutschen Worten neu (<see cref="IExtraktion.ParaphrasiereAsync"/>).
/// Dry-run zuerst (<see cref="SammelnAsync"/> erzeugt Vorschläge, schreibt nichts); <see cref="AnwendenAsync"/> speichert.
/// </summary>
public class TextBereinigungService(
    IExtraktion extraktion,
    IDbContextFactory<ApplicationDbContext> dbf,
    ILogger<TextBereinigungService> logger)
{
    public enum Art { BandBio, KklBeschreibung }

    public record Vorschlag(Art Art, Guid Id, string Name, string Alt, string Neu);

    /// <summary>Erzeugt Neufassungen (LLM) für die betroffenen Datensätze – ohne zu schreiben. Bei Band-Bios
    /// nur solche, die <b>englisch wirken</b> (WMC-Import); bei KKL alle KKL-Konzert-Beschreibungen.</summary>
    public async Task<IReadOnlyList<Vorschlag>> SammelnAsync(Art art, int max = 250, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var result = new List<Vorschlag>();

        if (art == Art.BandBio)
        {
            var bands = await db.Bands
                .Where(b => b.Geschichte != null && b.Geschichte != "")
                .Select(b => new { b.Id, b.Name, b.Geschichte })
                .ToListAsync(ct);
            foreach (var b in bands.Where(b => WirktEnglisch(b.Geschichte!)).Take(max))
            {
                ct.ThrowIfCancellationRequested();
                var neu = await extraktion.ParaphrasiereAsync(b.Geschichte!, ct);
                if (!string.IsNullOrWhiteSpace(neu) && neu != b.Geschichte)
                    result.Add(new Vorschlag(art, b.Id, b.Name, b.Geschichte!, neu!));
            }
        }
        else
        {
            var konzerte = await db.Konzerte
                .Where(k => k.Beschreibung != null && k.Beschreibung != "" && k.Ort != null && k.Ort.Contains("KKL Luzern"))
                .Select(k => new { k.Id, k.Name, k.Beschreibung })
                .ToListAsync(ct);
            foreach (var k in konzerte.Take(max))
            {
                ct.ThrowIfCancellationRequested();
                var neu = await extraktion.ParaphrasiereAsync(k.Beschreibung!, ct);
                if (!string.IsNullOrWhiteSpace(neu) && neu != k.Beschreibung)
                    result.Add(new Vorschlag(art, k.Id, k.Name ?? "(Konzert)", k.Beschreibung!, neu!));
            }
        }
        logger.LogInformation("Text-Bereinigung {Art}: {N} Vorschläge.", art, result.Count);
        return result;
    }

    /// <summary>Schreibt die (ggf. im Review reduzierten) Neufassungen in die Datenbank.</summary>
    public async Task<int> AnwendenAsync(IReadOnlyList<Vorschlag> vorschlaege, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var n = 0;
        foreach (var v in vorschlaege)
        {
            if (v.Art == Art.BandBio)
            {
                var b = await db.Bands.FindAsync([v.Id], ct);
                if (b != null) { b.Geschichte = v.Neu; n++; }
            }
            else
            {
                var k = await db.Konzerte.FindAsync([v.Id], ct);
                if (k != null) { k.Beschreibung = v.Neu; n++; }
            }
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Text-Bereinigung übernommen: {N} Datensätze.", n);
        return n;
    }

    /// <summary>Grobe Sprach-Heuristik: überwiegen englische Funktionswörter, gilt der Text als englisch.</summary>
    private static bool WirktEnglisch(string text)
    {
        var t = " " + text.ToLowerInvariant().Replace('\n', ' ') + " ";
        var en = Zaehle(t, "the", "and", "with", "for", "from", "has", "have", "was", "were", "been", "their", "which", "that", "are", "our", "we");
        var de = Zaehle(t, "der", "die", "das", "und", "ist", "wurde", "wurden", "mit", "für", "von", "sich", "auch", "eine", "werden", "haben", "sind");
        return en > de;
    }

    private static int Zaehle(string text, params string[] worte)
    {
        var n = 0;
        foreach (var w in worte)
        {
            var marker = " " + w + " ";
            var i = 0;
            while ((i = text.IndexOf(marker, i, StringComparison.Ordinal)) >= 0) { n++; i += marker.Length - 1; }
        }
        return n;
    }
}
