using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;
using HarmoniQ.Web.Services.Crawler;
using Microsoft.EntityFrameworkCore;

namespace HarmoniQ.Web.Services;

/// <summary>
/// YouTube-Crawler pro Band: geht die Uploads des hinterlegten YouTube-Kanals durch (sonst Fallback
/// Namenssuche über den Bandnamen), lässt das LLM aus Titel + Beschreibung Stück/Komponist:in/Ort/Anlass
/// vorschlagen und legt neue Treffer als <see cref="CrawlFund"/> vom Typ <see cref="CrawlFundTyp.Video"/>
/// (Status Offen) ab – im selben Review wie alle übrigen Funde. Kurze Clips (&lt; 2 Min, Trailer/Interviews)
/// werden aussortiert. Inkrementell: bereits erfasste Videos der Band und bereits vorhandene Video-Funde
/// (Dedup via <c>ExternKey "youtube:{bandId}:{externId}"</c>) werden übersprungen.
/// </summary>
public class BandVideoCrawlService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    YouTubeSearchService suche,
    IExtraktion extraktion,
    KomponistSuche komponistSuche,
    ILogger<BandVideoCrawlService> logger)
{
    /// <summary>Nur nutzbar, wenn ein YouTube-API-Key konfiguriert ist.</summary>
    public bool Verfuegbar => suche.Verfuegbar;

    /// <summary>Mindestlänge, damit Trailer/Interviews/Jingles aussortiert werden (Wunsch: „≥ 2 Minuten").</summary>
    private const int MinDauerSekunden = 120;

    /// <summary>Google-Such-Deckel für die Anreicherung (Stück/Komponist) pro Lauf – schützt das
    /// Gratis-Kontingent (100/Tag). Batch grosszügiger als eine Einzel-Band-Suche.</summary>
    private const int BatchSuchDeckel = 80;
    private const int EinzelSuchDeckel = 25;

    public record SuchBericht(int Neu, int Geprueft, bool ApiVerfuegbar, bool UeberKanal = false);

    public record BatchBericht(int Bands, int Geprueft, int Neu, bool ApiVerfuegbar);

    /// <summary>Gemeinsames Such-Budget + Titel-Cache über einen (ggf. bandübergreifenden) Lauf hinweg –
    /// begrenzt die Google-Suchen und vermeidet doppelte Komponisten-Lookups für dasselbe Stück.</summary>
    public sealed class SuchBudget(int deckel)
    {
        public int Rest { get; set; } = deckel;
        public Dictionary<string, string?> KomponistProTitel { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    // Titel-Bausteine, die auf ein GANZES Konzert / Nicht-Einzelstück hindeuten → Web-Suche überspringen.
    private static readonly string[] KeinEinzelstueck =
    [
        "jahreskonzert", "galakonzert", "unterhaltungsabend", "unterhaltungskonzert", "adventskonzert",
        "weihnachtskonzert", "neujahrskonzert", "kirchenkonzert", "frühlingskonzert", "fruehlingskonzert",
        "herbstkonzert", "muttertagskonzert", "trailer", "teaser", "interview", "aftermovie", "impression",
        "rückblick", "rueckblick", "highlight", "vlog", "rehearsal", "ständchen", "staendchen",
        "generalversammlung", "playlist", "livestream", "live stream", "ganzes konzert", "full concert"
    ];

    /// <summary>Grobe Heuristik, ob der Videotitel nach EINEM Stück aussieht (spart Kontingent bei
    /// offensichtlichen Ganz-Konzert-/Nicht-Musik-Videos). Präzision macht danach die grounded LLM-Auswertung.</summary>
    private static bool SiehtNachEinzelstueckAus(string titel)
    {
        if (string.IsNullOrWhiteSpace(titel) || titel.Length < 8) return false;
        var t = titel.ToLowerInvariant();
        return !KeinEinzelstueck.Any(m => t.Contains(m));
    }

    private static string FundKey(Guid bandId, string videoId) => $"youtube:{bandId}:{videoId}";

    /// <summary>Aggregat-Lauf „YouTube über alle Bands" (CrawlQuelleTyp.BandVideos): geht alle Bands mit
    /// hinterlegtem YouTube-Kanal durch und sucht je Band inkrementell neue Videos. Die Funde werden dem
    /// übergebenen Lauf zugeordnet. Gibt Summen zurück.</summary>
    public async Task<BatchBericht> AlleBandsAsync(Guid? laufId = null, CancellationToken ct = default)
    {
        if (!Verfuegbar) return new BatchBericht(0, 0, 0, false);
        List<Guid> bandIds;
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
            bandIds = await db.BandLinks
                .Where(l => l.Typ == LinkTyp.YouTube && l.Url != null && l.Url != "")
                .Select(l => l.BandId).Distinct().ToListAsync(ct);

        // Gemeinsames Such-Budget + Titel-Cache über ALLE Bands (Kontingent-Schutz, kein Doppel-Lookup).
        var budget = new SuchBudget(BatchSuchDeckel);
        int bands = 0, geprueft = 0, neu = 0;
        foreach (var id in bandIds)
        {
            ct.ThrowIfCancellationRequested();
            var b = await SuchenAsync(id, laufId: laufId, budget: budget, ct: ct);
            bands++; geprueft += b.Geprueft; neu += b.Neu;
        }
        logger.LogInformation("YouTube-Batch: {Bands} Bands mit Kanal, {Geprueft} Videos geprüft, {Neu} neue Funde.",
            bands, geprueft, neu);
        return new BatchBericht(bands, geprueft, neu, true);
    }

    /// <summary>
    /// Sucht neue YouTube-Kandidaten für die Band und persistiert sie als offene Video-<see cref="CrawlFund"/>e.
    /// Ist ein YouTube-Kanal an der Band hinterlegt (<see cref="LinkTyp.YouTube"/>), werden gezielt dessen
    /// Uploads durchgegangen (präzise + günstig); sonst Fallback auf die Suche über den Bandnamen.
    /// <paramref name="laufId"/> ordnet die Funde einem Lauf zu (Batch) oder ist null (on-demand pro Band).
    /// </summary>
    public async Task<SuchBericht> SuchenAsync(Guid bandId, int maxTreffer = 12, Guid? laufId = null,
        SuchBudget? budget = null, CancellationToken ct = default)
    {
        if (!Verfuegbar) return new SuchBericht(0, 0, false);
        budget ??= new SuchBudget(EinzelSuchDeckel);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var band = await db.Bands.FirstOrDefaultAsync(b => b.Id == bandId, ct);
        if (band is null) return new SuchBericht(0, 0, true);

        // Schon bekannt = bereits erfasste YouTube-Videos der Band + bereits vorhandene Video-Funde (ExternKey).
        var bekannteVideos = await db.Videos
            .Where(v => v.BandId == bandId && v.Plattform == VideoPlattform.YouTube)
            .Select(v => v.ExternId).ToListAsync(ct);
        var prefix = $"youtube:{bandId}:";
        var bekannteFundKeys = await db.CrawlFunde
            .Where(f => f.Typ == CrawlFundTyp.Video && f.ExternKey != null && f.ExternKey.StartsWith(prefix))
            .Select(f => f.ExternKey!).ToListAsync(ct);
        var bekannt = new HashSet<string>(bekannteVideos, StringComparer.Ordinal);
        foreach (var k in bekannteFundKeys) bekannt.Add(k[prefix.Length..]);

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

        // Nur wirklich neue Video-IDs weiterverarbeiten.
        var neueTreffer = treffer.Where(t => !string.IsNullOrWhiteSpace(t.VideoId) && bekannt.Add(t.VideoId)).ToList();

        // Dauer + Beschreibung nachladen: kurze Clips (Trailer/Interviews/Jingles) < 2 Min aussortieren,
        // die Beschreibung hilft dem LLM bei Ort/Anlass. 1 Kontingent-Einheit je 50 Videos.
        var details = await suche.VideoDetailsAsync(neueTreffer.Select(t => t.VideoId).ToList(), ct);

        int neu = 0, zuKurz = 0;
        foreach (var t in neueTreffer)
        {
            details.TryGetValue(t.VideoId, out var det);
            // Bekannte Dauer < Mindestlänge → überspringen; unbekannte Dauer (0) im Zweifel behalten.
            if (det is { DauerSekunden: > 0 } && det.DauerSekunden < MinDauerSekunden) { zuKurz++; continue; }

            VideoAnalyse analyse;
            try
            {
                analyse = await extraktion.VideoTitelAnalysierenAsync(t.Titel, band.Name, det?.Beschreibung, ct);
            }
            catch (Exception ex)
            {
                // Analyse ist Best-effort – Fund trotzdem anlegen (Felder in der Review manuell setzen).
                logger.LogWarning(ex, "Titel-Analyse für {VideoId} fehlgeschlagen.", t.VideoId);
                analyse = new VideoAnalyse(null, null);
            }

            // Web-Suche-Anreicherung (grounded, kontingent-gedeckelt), wenn Titel/Beschreibung zu wenig hergaben.
            var (stueck, komponist) = await AnreichernAsync(t.Titel, band.Name, analyse.StueckTitel, analyse.Komponist, budget, ct);

            var daten = new VideoFundDaten(t.VideoId, t.Titel, bandId, band.Name, t.Kanal,
                stueck, komponist, analyse.Ort, analyse.Anlass);
            db.CrawlFunde.Add(new CrawlFund
            {
                LaufId = laufId,
                Typ = CrawlFundTyp.Video,
                ExternKey = FundKey(bandId, t.VideoId),
                QuellUrl = $"https://youtu.be/{t.VideoId}",
                AbgerufenAm = DateTime.UtcNow,
                DatenJson = CrawlDaten.Serialisiere(daten),
                Konfidenz = Konfidenz.Mittel,
                Status = CrawlFundStatus.Offen
            });
            neu++;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("YouTube {Modus} Band {Band}: {Geprueft} geprüft, {Neu} neu, {Kurz} zu kurz (<2 Min).",
            ueberKanal ? "Kanal" : "Namenssuche", band.Name, treffer.Count, neu, zuKurz);
        return new SuchBericht(neu, treffer.Count, true, ueberKanal);
    }

    /// <summary>Ergänzt Stück/Komponist über die grounded Web-Suche, wenn Titel+Beschreibung zu wenig hergaben.
    /// B: kein Stück → Videotitel googeln (nur bei Einzelstück-Heuristik). A: Stück ohne Komponist → nachschlagen
    /// (gecacht pro Titel). Beides nur solange Such-Budget reicht und die Suche (GoogleCx) aktiv ist.</summary>
    private async Task<(string? Stueck, string? Komponist)> AnreichernAsync(
        string videoTitel, string? bandName, string? stueck, string? komponist, SuchBudget budget, CancellationToken ct)
    {
        if (!komponistSuche.Aktiv || budget.Rest <= 0) return (stueck, komponist);

        // B) Kein Stück erkannt → Videotitel googeln (nur wenn er nach einem Einzelstück aussieht).
        if (stueck is null && SiehtNachEinzelstueckAus(videoTitel))
        {
            budget.Rest--;
            var r = await komponistSuche.StueckAusVideoAsync(videoTitel, bandName, ct);
            if (!string.IsNullOrWhiteSpace(r?.StueckTitel))
            {
                stueck = r!.StueckTitel;
                komponist ??= r.Komponist;
            }
        }

        // A) Stück bekannt, aber Komponist fehlt → per Suche nachschlagen (gecacht pro Titel, spart Kontingent).
        if (stueck is not null && string.IsNullOrWhiteSpace(komponist))
        {
            if (budget.KomponistProTitel.TryGetValue(stueck, out var gecacht))
                komponist = gecacht;
            else if (budget.Rest > 0)
            {
                budget.Rest--;
                komponist = await komponistSuche.KomponistAsync(stueck, ct);
                budget.KomponistProTitel[stueck] = komponist;
            }
        }

        return (stueck, komponist);
    }
}
