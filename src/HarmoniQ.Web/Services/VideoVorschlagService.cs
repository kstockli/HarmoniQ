using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Nutzer-Vorschlag einer Aufnahme (UX: „Video vorschlagen"). Reihenfolge Link → Band → Stück.
/// Legt fehlende Band/Stück/Komponist:in per Find-or-create an (HarmoniQ kennt noch nicht alle Stücke);
/// das Video selbst wird als <see cref="VideoStatus.Ausstehend"/> erfasst und von einer Admin-Person geprüft.
/// Unterstützt YouTube UND direkte Datei-URLs (<see cref="VideoQuelle"/>).
/// </summary>
public static class VideoVorschlagService
{
    public record Eingabe(string Url, string? Titel, string? BandName, string StueckTitel, string? KomponistName);
    public record Ergebnis(bool Ok, string Meldung, Guid? VideoId, string? ErkannteBand);

    public static async Task<Ergebnis> ErstelleAsync(
        ApplicationDbContext db, Eingabe e, string? userId, YouTubeMetadataService youtube)
    {
        if (VideoQuelle.Parse(e.Url) is not { } quelle)
            return new(false, "Bitte einen gültigen YouTube-Link/-ID oder eine direkte Datei-URL angeben.", null, null);
        if (string.IsNullOrWhiteSpace(e.StueckTitel))
            return new(false, "Bitte das gespielte Stück angeben (bestehend wählen oder neu eintippen).", null, null);

        // Metadaten nur bei YouTube (Titel-/Band-Fallback).
        YouTubeMetadataService.Metadaten? meta = quelle.Plattform == VideoPlattform.YouTube
            ? await youtube.HoleAsync(quelle.ExternId) : null;

        var titel = Leer(e.Titel) ?? meta?.Titel
            ?? (quelle.Plattform == VideoPlattform.Datei ? VideoQuelle.TitelAusDateiUrl(quelle.ExternId) : null)
            ?? "(ohne Titel)";

        // ── Stück (find-or-create; optional Komponist:in) ──────────────────────
        var stTitel = e.StueckTitel.Trim();
        var stueck = await db.Stuecke.FirstOrDefaultAsync(s => s.Titel == stTitel || s.Aliase.Any(a => a.Name == stTitel));
        if (stueck == null)
        {
            stueck = new Stueck { Titel = stTitel };
            db.Stuecke.Add(stueck);
            foreach (var b in KomponistParser.Parse(e.KomponistName))
                db.StueckBeitraege.Add(new StueckBeitrag
                {
                    Stueck = stueck, Person = await FindeOderErstellePersonAsync(db, b.Name), Rolle = b.Rolle
                });
        }

        // Dedup: gleiches Video am gleichen Stück nicht doppelt.
        if (await db.Videos.AnyAsync(v => v.ExternId == quelle.ExternId && v.StueckId == stueck.Id))
            return new(false, "Dieses Video ist für dieses Stück bereits erfasst oder vorgeschlagen.", null, null);

        // ── Band (explizit gewählt/neu; sonst aus dem YouTube-Kanal) ───────────
        Guid? bandId = null; string? bandName = null;
        if (Leer(e.BandName) is { } bn)
        {
            var band = await FindeOderErstelleBandAsync(db, bn);
            bandId = band.Id; bandName = band.Name;
        }
        else if (meta != null)
        {
            var band = await FindeOderErstelleBandAusKanalAsync(db, meta);
            bandId = band?.Id; bandName = band?.Name;
        }

        var video = new Video
        {
            StueckId = stueck.Id, BandId = bandId,
            Plattform = quelle.Plattform, ExternId = quelle.ExternId,
            Titel = titel, Status = VideoStatus.Ausstehend, VorgeschlagenVonId = userId
        };
        db.Videos.Add(video);
        await db.SaveChangesAsync();

        if (userId != null)
            await AktivitaetService.ProtokolliereFuerBenutzerAsync(db, userId,
                AktivitaetTyp.VideoHinzugefuegt, AktivitaetZielTyp.Video, video.Id);

        return new(true, "Danke! Dein Vorschlag wird von einer Admin-Person geprüft.", video.Id, bandName);
    }

    private static async Task<Band> FindeOderErstelleBandAsync(ApplicationDbContext db, string name)
    {
        name = name.Trim();
        var band = await db.Bands.FirstOrDefaultAsync(b => b.Name == name)
            ?? await db.Bands.FirstOrDefaultAsync(b => b.Aliase.Any(a => a.Name == name));
        if (band == null) { band = new Band { Name = name }; db.Bands.Add(band); }
        return band;
    }

    private static async Task<Band?> FindeOderErstelleBandAusKanalAsync(ApplicationDbContext db, YouTubeMetadataService.Metadaten meta)
    {
        if (!string.IsNullOrWhiteSpace(meta.KanalUrl))
        {
            var perUrl = await db.Bands.FirstOrDefaultAsync(b => b.Webseite != null && b.Webseite == meta.KanalUrl);
            if (perUrl != null) return perUrl;
        }
        if (string.IsNullOrWhiteSpace(meta.KanalName)) return null;
        var perName = await db.Bands.FirstOrDefaultAsync(b => b.Name == meta.KanalName);
        if (perName != null) return perName;
        var neu = new Band { Name = meta.KanalName!, Webseite = meta.KanalUrl };
        db.Bands.Add(neu);
        return neu;
    }

    private static async Task<Person> FindeOderErstellePersonAsync(ApplicationDbContext db, string name)
    {
        name = name.Trim();
        var person = await db.Personen.Include(p => p.Rollen).FirstOrDefaultAsync(p => p.Name == name)
            ?? await db.Personen.Include(p => p.Rollen).FirstOrDefaultAsync(p => p.Aliase.Any(a => a.Name == name));
        if (person == null)
        {
            person = new Person { Name = name, Sichtbarkeit = Sichtbarkeit.Oeffentlich };
            db.Personen.Add(person);
        }
        if (person.Rollen.All(r => r.Rolle != PersonRolleTyp.Komponist))
            person.Rollen.Add(new PersonRolle { Rolle = PersonRolleTyp.Komponist });
        return person;
    }

    private static string? Leer(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
