using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Führt ein Quell-Konzert in ein Ziel-Konzert zusammen („hineinschmelzen"). Teilnehmende Bands
/// (<see cref="KonzertBand"/>), Programm (<see cref="KonzertStueck"/>), Mitwirkende
/// (<see cref="KonzertPerson"/>) und Videos werden auf das Ziel-Konzert umgehängt – ohne Dubletten.
/// Danach wird das Quell-Konzert gelöscht. (Konzerte brauchen keine Aliase.)
/// </summary>
public static class KonzertMergeService
{
    public static async Task<(bool Ok, string Meldung)> MergeAsync(ApplicationDbContext db, Guid quelleId, Guid zielId)
    {
        if (quelleId == zielId) return (false, "Quelle und Ziel sind identisch.");

        var quelle = await db.Konzerte.FirstOrDefaultAsync(k => k.Id == quelleId);
        var ziel = await db.Konzerte.FirstOrDefaultAsync(k => k.Id == zielId);
        if (quelle == null || ziel == null) return (false, "Konzert nicht gefunden.");

        // ── Videos: umhängen ──────────────────────────────────────────────────
        foreach (var v in await db.Videos.Where(v => v.KonzertId == quelleId).ToListAsync())
            v.KonzertId = zielId;

        // ── KonzertBand (PK Konzert+Band): fehlende ergänzen, Quelle entfernen ─
        var zielBands = await db.KonzertBands.Where(kb => kb.KonzertId == zielId)
            .Select(kb => kb.BandId).ToHashSetAsync();
        foreach (var kb in await db.KonzertBands.Where(kb => kb.KonzertId == quelleId).ToListAsync())
        {
            if (!zielBands.Contains(kb.BandId))
            {
                db.KonzertBands.Add(new KonzertBand { KonzertId = zielId, BandId = kb.BandId, Rang = kb.Rang, Punkte = kb.Punkte });
                zielBands.Add(kb.BandId);
            }
            db.KonzertBands.Remove(kb);
        }

        // ── KonzertStueck (unique Konzert+Stück+Band): umhängen, Dublette verwerfen ─
        var zielProgramm = (await db.KonzertStuecke.Where(ks => ks.KonzertId == zielId)
            .Select(ks => new { ks.StueckId, ks.BandId }).ToListAsync())
            .Select(x => (x.StueckId, x.BandId)).ToHashSet();
        foreach (var ks in await db.KonzertStuecke.Where(ks => ks.KonzertId == quelleId).ToListAsync())
        {
            if (zielProgramm.Contains((ks.StueckId, ks.BandId))) db.KonzertStuecke.Remove(ks);
            else { ks.KonzertId = zielId; zielProgramm.Add((ks.StueckId, ks.BandId)); }
        }

        // ── KonzertPerson (unique Konzert+Person+Rolle): umhängen, Dublette verwerfen ─
        var zielKp = (await db.KonzertPersonen.Where(kp => kp.KonzertId == zielId)
            .Select(kp => new { kp.PersonId, kp.Rolle }).ToListAsync())
            .Select(x => (x.PersonId, x.Rolle)).ToHashSet();
        foreach (var kp in await db.KonzertPersonen.Where(kp => kp.KonzertId == quelleId).ToListAsync())
        {
            if (zielKp.Contains((kp.PersonId, kp.Rolle))) db.KonzertPersonen.Remove(kp);
            else { kp.KonzertId = zielId; zielKp.Add((kp.PersonId, kp.Rolle)); }
        }

        // ── Stammdaten-Lücken des Ziels aus der Quelle füllen (Datum bleibt Ziel) ─
        if (string.IsNullOrWhiteSpace(ziel.Name)) ziel.Name = quelle.Name;
        if (string.IsNullOrWhiteSpace(ziel.Ort)) ziel.Ort = quelle.Ort;
        if (string.IsNullOrWhiteSpace(ziel.Beschreibung)) ziel.Beschreibung = quelle.Beschreibung;
        ziel.BildUrl ??= quelle.BildUrl;

        db.Konzerte.Remove(quelle);
        await db.SaveChangesAsync();
        return (true, $"„{Bez(quelle)}“ wurde in „{Bez(ziel)}“ zusammengeführt.");
    }

    private static string Bez(Konzert k) => k.Name ?? k.Datum.ToString("dd.MM.yyyy");
}
