using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;
using HarmoniQ.Web.Services.Crawler;
using Microsoft.EntityFrameworkCore;

namespace HarmoniQ.Web.Services;

/// <summary>
/// YouTube-Crawler pro Band (Band-Admin, on-demand): sucht über den Bandnamen bei YouTube, lässt das
/// LLM aus jedem Videotitel Stück + Komponist:in vorschlagen und legt neue Treffer als
/// <see cref="BandVideoFund"/> (Status Offen) ab. Inkrementell: bereits erfasste Videos der Band und
/// bereits gefundene Kandidaten (egal ob offen/entschieden) werden übersprungen – ein erneuter
/// Suchlauf liefert also nur wirklich Neues. Die eigentliche Übernahme/Ablehnung erfolgt in der Review.
/// </summary>
public class BandVideoCrawlService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    YouTubeSearchService suche,
    IExtraktion extraktion,
    ILogger<BandVideoCrawlService> logger)
{
    /// <summary>Nur nutzbar, wenn ein YouTube-API-Key konfiguriert ist.</summary>
    public bool Verfuegbar => suche.Verfuegbar;

    public record SuchBericht(int Neu, int Geprueft, bool ApiVerfuegbar, bool UeberKanal = false);

    /// <summary>
    /// Sucht neue YouTube-Kandidaten für die Band und persistiert sie als offene Funde.
    /// Ist ein YouTube-Kanal an der Band hinterlegt (<see cref="LinkTyp.YouTube"/>), werden gezielt dessen
    /// Uploads durchgegangen (präzise + günstig); sonst Fallback auf die Suche über den Bandnamen.
    /// Gibt zurück, wie viele Treffer geprüft und wie viele neu angelegt wurden.
    /// </summary>
    public async Task<SuchBericht> SuchenAsync(Guid bandId, int maxTreffer = 12, CancellationToken ct = default)
    {
        if (!Verfuegbar) return new SuchBericht(0, 0, false);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var band = await db.Bands.FirstOrDefaultAsync(b => b.Id == bandId, ct);
        if (band is null) return new SuchBericht(0, 0, true);

        // Schon bekannt = bereits erfasste YouTube-Videos der Band + bereits gefundene Kandidaten.
        var bekannteVideos = await db.Videos
            .Where(v => v.BandId == bandId && v.Plattform == VideoPlattform.YouTube)
            .Select(v => v.ExternId).ToListAsync(ct);
        var bekannteFunde = await db.BandVideoFunde
            .Where(f => f.BandId == bandId).Select(f => f.ExternId).ToListAsync(ct);
        var bekannt = new HashSet<string>(bekannteVideos.Concat(bekannteFunde), StringComparer.Ordinal);

        // Kanal bevorzugen (präzise + 1 statt 100 Kontingent-Einheiten), sonst Namenssuche.
        var kanalUrl = await db.BandLinks
            .Where(l => l.BandId == bandId && l.Typ == LinkTyp.YouTube)
            .Select(l => l.Url).FirstOrDefaultAsync(ct);

        List<YouTubeSearchService.Treffer> treffer = [];
        bool ueberKanal = false;
        if (!string.IsNullOrWhiteSpace(kanalUrl))
        {
            treffer = await suche.KanalVideosAsync(kanalUrl, Math.Max(maxTreffer, 25), ct);
            ueberKanal = treffer.Count > 0;
        }
        if (treffer.Count == 0)
        {
            var ergebnis = await suche.SucheAsync(band.Name, maxTreffer, ct: ct);
            treffer = ergebnis.Treffer;
        }

        int neu = 0;
        foreach (var t in treffer)
        {
            if (string.IsNullOrWhiteSpace(t.VideoId) || !bekannt.Add(t.VideoId)) continue;

            VideoAnalyse analyse;
            try
            {
                analyse = await extraktion.VideoTitelAnalysierenAsync(t.Titel, band.Name, ct);
            }
            catch (Exception ex)
            {
                // Analyse ist Best-effort – Fund trotzdem anlegen (Felder in der Review manuell setzen).
                logger.LogWarning(ex, "Titel-Analyse für {VideoId} fehlgeschlagen.", t.VideoId);
                analyse = new VideoAnalyse(null, null);
            }

            db.BandVideoFunde.Add(new BandVideoFund
            {
                BandId = bandId,
                ExternId = t.VideoId,
                Titel = t.Titel,
                KanalName = t.Kanal,
                StueckVorschlag = analyse.StueckTitel,
                KomponistVorschlag = analyse.Komponist
            });
            neu++;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("YouTube {Modus} Band {Band}: {Geprueft} geprüft, {Neu} neu.",
            ueberKanal ? "Kanal" : "Namenssuche", band.Name, treffer.Count, neu);
        return new SuchBericht(neu, treffer.Count, true, ueberKanal);
    }
}
