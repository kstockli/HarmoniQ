using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Stellt den <b>kanal-neutralen</b> Wochen-Digest eines Kontos zusammen (Wiederkehr-Schleife,
/// UX-Spec 4.2): Bausteine A (kommende Konzerte), B (Tagebuch-Nachfrage) und C (neue Videos) aus den
/// Bands, die das Konto interessieren = <b>Mitgliedschaft ∪ Folgen</b>. Bereits verschickte Bausteine
/// (Dedup-Log <see cref="BenachrichtigungGesendet"/>) werden herausgefiltert. Der Versand (E-Mail/Push)
/// und das Schreiben des Logs passieren in den Kanal-Adaptern (Punkt 3/4), nicht hier.
/// </summary>
public static class DigestService
{
    // Zeitfenster der Bausteine.
    private const int KommendeTage = 30;   // A: Konzerte in den nächsten … Tagen
    private const int RueckblickTage = 14; // B: vergangene Konzerte der letzten … Tage
    private const int VideoTage = 14;      // C: Videos der letzten … Tage
    private const double NaeheKm = 30.0;   // F: Umkreis für „in deiner Nähe"
    private const int NaheMax = 5;         // F: höchstens so viele Nähe-Konzerte

    public record Posten(BenachrichtigungTyp Typ, Guid EntitaetId, string Titel, string Detail, string Href);

    public record Digest(List<Posten> Kommende, List<Posten> Nachfragen, List<Posten> Videos, List<Posten> Nahe)
    {
        public int Total => Kommende.Count + Nachfragen.Count + Videos.Count + Nahe.Count;
        public bool Leer => Total == 0;
        public IEnumerable<Posten> Alle => Kommende.Concat(Nachfragen).Concat(Videos).Concat(Nahe);
    }

    private static readonly Digest Leer = new([], [], [], []);

    /// <summary>Band-Ids, die das Konto interessieren: Mitgliedschaft ∪ Folgen (der verknüpften Person).</summary>
    public static async Task<HashSet<Guid>> InteressierteBandIdsAsync(ApplicationDbContext db, string? userId)
    {
        if (string.IsNullOrEmpty(userId)) return [];
        var personId = await db.Personen.Where(p => p.BenutzerId == userId)
            .Select(p => (Guid?)p.Id).FirstOrDefaultAsync();
        if (personId is not { } pid) return [];

        var mitglied = await db.BandMitgliedschaften.Where(m => m.PersonId == pid).Select(m => m.BandId).ToListAsync();
        var folgt = await db.BandInteressen.Where(i => i.PersonId == pid).Select(i => i.BandId).ToListAsync();
        return mitglied.Concat(folgt).ToHashSet();
    }

    /// <param name="nurUngesehene">true (Standard, für den Versand): bereits verschickte Bausteine
    /// (Dedup-Log) ausblenden. false (für den Live-Feed auf der Startseite): alles Aktuelle zeigen.</param>
    public static async Task<Digest> ErstelleAsync(ApplicationDbContext db, string? userId, bool nurUngesehene = true)
    {
        if (string.IsNullOrEmpty(userId)) return Leer;

        var bandIds = await InteressierteBandIdsAsync(db, userId);
        if (bandIds.Count == 0) return Leer;

        // Bereits verschickte Bausteine (Typ + EntitätsId) zum Filtern (nur beim Versand).
        var gesendet = nurUngesehene
            ? (await db.BenachrichtigungenGesendet
                    .Where(g => g.BenutzerId == userId)
                    .Select(g => new { g.Typ, g.EntitaetId })
                    .ToListAsync())
                .Select(g => (g.Typ, g.EntitaetId)).ToHashSet()
            : [];
        bool Neu(BenachrichtigungTyp t, Guid id) => !nurUngesehene || !gesendet.Contains((t, id));

        var heute = DateOnly.FromDateTime(DateTime.Today);

        // ── A: kommende Konzerte ───────────────────────────────────────────────
        var bisA = heute.AddDays(KommendeTage);
        var kommendeRoh = await db.KonzertBands
            .Where(kb => bandIds.Contains(kb.BandId) && kb.Konzert.Datum >= heute && kb.Konzert.Datum <= bisA)
            .Select(kb => new { kb.KonzertId, kb.Konzert.Datum, kb.Konzert.Uhrzeit, kb.Konzert.Name, kb.Konzert.Ort, Band = kb.Band.Name })
            .ToListAsync();
        var kommende = kommendeRoh
            .GroupBy(x => x.KonzertId)
            .Where(g => Neu(BenachrichtigungTyp.KommendesKonzert, g.Key))
            .OrderBy(g => g.Min(x => x.Datum))
            .Select(g =>
            {
                var e = g.First();
                var bands = string.Join(", ", g.Select(x => x.Band).Distinct());
                var detail = e.Datum.ToString("dd.MM.yyyy") + KonzertZeitFormat.ZeitZusatz(e.Uhrzeit, mitUhr: false)
                    + (string.IsNullOrWhiteSpace(e.Ort) ? "" : $" · {e.Ort}") + $" · {bands}";
                return new Posten(BenachrichtigungTyp.KommendesKonzert, g.Key,
                    e.Name ?? e.Datum.ToString("dd.MM.yyyy"), detail, $"/konzerte/{g.Key}");
            })
            .ToList();

        // ── B: Tagebuch-Nachfrage zu kürzlich vergangenen Konzerten ────────────
        var vonB = heute.AddDays(-RueckblickTage);
        var besucht = (await db.KonzertBesuche.Where(x => x.BenutzerId == userId)
            .Select(x => x.KonzertId).ToListAsync()).ToHashSet();
        var vergangenRoh = await db.KonzertBands
            .Where(kb => bandIds.Contains(kb.BandId) && kb.Konzert.Datum >= vonB && kb.Konzert.Datum < heute)
            .Select(kb => new { kb.KonzertId, kb.Konzert.Datum, kb.Konzert.Uhrzeit, kb.Konzert.Name, kb.Konzert.Ort, Band = kb.Band.Name })
            .ToListAsync();
        var nachfragen = vergangenRoh
            .GroupBy(x => x.KonzertId)
            .Where(g => !besucht.Contains(g.Key) && Neu(BenachrichtigungTyp.TagebuchNachfrage, g.Key))
            .OrderByDescending(g => g.Min(x => x.Datum))
            .Select(g =>
            {
                var e = g.First();
                var bands = string.Join(", ", g.Select(x => x.Band).Distinct());
                var detail = e.Datum.ToString("dd.MM.yyyy") + KonzertZeitFormat.ZeitZusatz(e.Uhrzeit, mitUhr: false)
                    + (string.IsNullOrWhiteSpace(e.Ort) ? "" : $" · {e.Ort}") + $" · {bands}";
                return new Posten(BenachrichtigungTyp.TagebuchNachfrage, g.Key,
                    e.Name ?? e.Datum.ToString("dd.MM.yyyy"), detail, $"/konzerte/{g.Key}");
            })
            .ToList();

        // ── C: neue Videos ─────────────────────────────────────────────────────
        var videoCut = DateTime.UtcNow.AddDays(-VideoTage);
        var videoRoh = await db.Videos
            .Where(v => v.BandId != null && bandIds.Contains(v.BandId.Value)
                && v.Status == VideoStatus.Genehmigt && v.CreateTime >= videoCut)
            .OrderByDescending(v => v.CreateTime)
            .Select(v => new { v.Id, v.Titel, Stueck = v.Stueck.Titel, Band = v.Band!.Name })
            .ToListAsync();
        var videos = videoRoh
            .Where(v => Neu(BenachrichtigungTyp.NeuesVideo, v.Id))
            .Select(v => new Posten(BenachrichtigungTyp.NeuesVideo, v.Id,
                v.Stueck, $"{v.Band} · {v.Titel}", $"/videos/{v.Id}"))
            .ToList();

        // ── F: kommende Konzerte in der Nähe (fremde Bands; nur mit Heim-Standort) ─────────
        var nahe = new List<Posten>();
        var standort = await db.Personen.Where(p => p.BenutzerId == userId)
            .Select(p => new { p.StandortLat, p.StandortLng }).FirstOrDefaultAsync();
        if (standort is { StandortLat: double hLat, StandortLng: double hLng })
        {
            var meineKonzerte = kommendeRoh.Select(x => x.KonzertId).ToHashSet();
            var kandidaten = await db.Konzerte
                .Where(k => k.Datum >= heute && k.Datum <= bisA
                    && k.Lokal != null && k.Lokal.Lat != null && k.Lokal.Lng != null)
                .Select(k => new
                {
                    k.Id, k.Datum, k.Uhrzeit, k.Name,
                    Ort = k.Lokal!.Name, Lat = k.Lokal.Lat!.Value, Lng = k.Lokal.Lng!.Value,
                    Bands = k.Bands.Select(b => b.Band.Name).ToList()
                })
                .ToListAsync();

            nahe = kandidaten
                .Where(k => !meineKonzerte.Contains(k.Id) && Neu(BenachrichtigungTyp.NahesKonzert, k.Id))
                .Select(k => new { k, Km = DistanzKm(hLat, hLng, k.Lat, k.Lng) })
                .Where(x => x.Km <= NaeheKm)
                .OrderBy(x => x.k.Datum).ThenBy(x => x.Km)
                .Take(NaheMax)
                .Select(x =>
                {
                    var bands = string.Join(", ", x.k.Bands.Distinct());
                    var detail = x.k.Datum.ToString("dd.MM.yyyy") + KonzertZeitFormat.ZeitZusatz(x.k.Uhrzeit, mitUhr: false)
                        + $" · {x.k.Ort} · ~{Math.Round(x.Km)} km"
                        + (string.IsNullOrWhiteSpace(bands) ? "" : $" · {bands}");
                    return new Posten(BenachrichtigungTyp.NahesKonzert, x.k.Id,
                        x.k.Name ?? x.k.Datum.ToString("dd.MM.yyyy"), detail, $"/konzerte/{x.k.Id}");
                })
                .ToList();
        }

        return new Digest(kommende, nachfragen, videos, nahe);
    }

    private static double DistanzKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371.0;
        double Rad(double g) => g * Math.PI / 180.0;
        var dLat = Rad(lat2 - lat1);
        var dLng = Rad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
