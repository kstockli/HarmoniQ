using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Einmaliges Befüllen der <see cref="Aktivitaet"/>-Tabelle aus bestehenden Daten (Bewertungen,
/// hinzugefügte Videos, bestätigte Freundschaften) – mit den jeweils echten Zeitstempeln, damit
/// der Feed nicht „leer“ startet. Läuft nur, wenn die Tabelle noch leer ist.
/// </summary>
public static class AktivitaetBackfill
{
    public static async Task RunAsync(ApplicationDbContext db)
    {
        if (await db.Aktivitaeten.AnyAsync()) return;

        var personByUser = await db.Personen
            .Where(p => p.BenutzerId != null)
            .ToDictionaryAsync(p => p.BenutzerId!, p => p.Id);

        var neu = new List<Aktivitaet>();

        // Bewertungen eingeloggter Nutzer:innen.
        var bews = await db.Bewertungen
            .Where(b => b.BenutzerId != null)
            .Select(b => new { b.BenutzerId, b.VideoId, b.ErstelltAm })
            .ToListAsync();
        foreach (var b in bews)
            if (personByUser.TryGetValue(b.BenutzerId!, out var pid))
                neu.Add(new Aktivitaet
                {
                    AkteurPersonId = pid,
                    Typ = AktivitaetTyp.BewertungAbgegeben,
                    ZielTyp = AktivitaetZielTyp.Video,
                    ZielId = b.VideoId,
                    Zeitpunkt = b.ErstelltAm
                });

        // Hinzugefügte (genehmigte) Videos mit bekannter vorschlagender Person.
        var vids = await db.Videos
            .Where(v => v.VorgeschlagenVonId != null && v.Status == VideoStatus.Genehmigt)
            .Select(v => new { v.VorgeschlagenVonId, v.Id, v.ErstelltAm })
            .ToListAsync();
        foreach (var v in vids)
            if (personByUser.TryGetValue(v.VorgeschlagenVonId!, out var pid))
                neu.Add(new Aktivitaet
                {
                    AkteurPersonId = pid,
                    Typ = AktivitaetTyp.VideoHinzugefuegt,
                    ZielTyp = AktivitaetZielTyp.Video,
                    ZielId = v.Id,
                    Zeitpunkt = v.ErstelltAm
                });

        // Bestätigte Freundschaften.
        var fs = await db.Freundschaften
            .Where(f => f.Status == FreundschaftStatus.Bestaetigt)
            .Select(f => new { f.EmpfaengerPersonId, f.AnfragerPersonId, f.EntschiedenAm, f.ErstelltAm })
            .ToListAsync();
        foreach (var f in fs)
            neu.Add(new Aktivitaet
            {
                AkteurPersonId = f.EmpfaengerPersonId,
                Typ = AktivitaetTyp.FreundschaftBestaetigt,
                NebenPersonId = f.AnfragerPersonId,
                Zeitpunkt = f.EntschiedenAm ?? f.ErstelltAm
            });

        if (neu.Count == 0) return;
        db.Aktivitaeten.AddRange(neu);
        await db.SaveChangesAsync();
    }
}
