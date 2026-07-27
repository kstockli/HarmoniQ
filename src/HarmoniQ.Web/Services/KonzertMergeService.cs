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

        // ── KonzertStueck: umhängen; bei Dublette Programm-Zeile verwerfen, aber private
        //    StueckEindruck-Einträge auf die Ziel-Zeile RETTEN (nicht per Cascade verlieren). ─
        var zielRows = await db.KonzertStuecke.Where(ks => ks.KonzertId == zielId).ToListAsync();
        var zielNachSchluessel = new Dictionary<(Guid, Guid?), Guid>();
        foreach (var zr in zielRows) zielNachSchluessel.TryAdd((zr.StueckId, zr.BandId), zr.Id);

        foreach (var ks in await db.KonzertStuecke.Where(ks => ks.KonzertId == quelleId).ToListAsync())
        {
            if (zielNachSchluessel.TryGetValue((ks.StueckId, ks.BandId), out var zielKsId))
            {
                await EindrueckeUmhaengenAsync(db, ks.Id, zielKsId);
                db.KonzertStuecke.Remove(ks);
            }
            else
            {
                ks.KonzertId = zielId;
                zielNachSchluessel[(ks.StueckId, ks.BandId)] = ks.Id;
            }
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

        // ── KonzertBesuch (Tagebuch): auf das Ziel-Konzert umhängen, sonst gingen die Besuche
        //    beim Löschen des Quell-Konzerts per Cascade verloren. Dedup pro Nutzer:in. ─
        var zielBesuchUser = (await db.KonzertBesuche.Where(b => b.KonzertId == zielId)
            .Select(b => b.BenutzerId).ToListAsync()).ToHashSet();
        foreach (var b in await db.KonzertBesuche.Where(b => b.KonzertId == quelleId).ToListAsync())
        {
            if (zielBesuchUser.Add(b.BenutzerId)) b.KonzertId = zielId;
            else db.KonzertBesuche.Remove(b);   // Nutzer:in hat das Ziel-Konzert bereits eingetragen
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

    /// <summary>Hängt private StueckEindruck-Einträge von einer Programm-Zeile auf eine andere um
    /// (bei Konzert-Merge). Hat ein:e Nutzer:in auf der Ziel-Zeile bereits einen Eindruck, wird der
    /// Quell-Eintrag verworfen (Unique-Index BenutzerId+KonzertStueckId).</summary>
    private static async Task EindrueckeUmhaengenAsync(ApplicationDbContext db, Guid vonKsId, Guid nachKsId)
    {
        var zielUser = (await db.StueckEindruecke.Where(s => s.KonzertStueckId == nachKsId)
            .Select(s => s.BenutzerId).ToListAsync()).ToHashSet();
        foreach (var s in await db.StueckEindruecke.Where(s => s.KonzertStueckId == vonKsId).ToListAsync())
        {
            if (zielUser.Add(s.BenutzerId)) s.KonzertStueckId = nachKsId;
            else db.StueckEindruecke.Remove(s);
        }
    }
}
