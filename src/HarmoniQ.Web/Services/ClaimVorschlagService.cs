using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Claim „Modell B" (UX-Spec Block 3): schlägt der verknüpften Selbst-Person proaktiv einen
/// <b>Merge</b> mit einer bereits erfassten (unverknüpften) Person vor – aber nur bei <b>starker
/// Konfidenz</b> (Name-Gleichheit normalisiert <b>+ gemeinsame Band-Mitgliedschaft</b>), damit keine
/// fremde Identitäts-Pickliste entsteht. Für sichtbare Rollen greift zusätzlich das Verifizierungs-Gate.
/// </summary>
public static class ClaimVorschlagService
{
    public record Kandidat(Guid Id, string Name, string Bands, bool Sichtbar);

    public static async Task<Kandidat?> FindeAsync(ApplicationDbContext db, string? userId)
    {
        if (string.IsNullOrEmpty(userId)) return null;

        var selbst = await db.Personen
            .Include(p => p.Bandmitgliedschaften)
            .FirstOrDefaultAsync(p => p.BenutzerId == userId);
        if (selbst == null) return null;

        var bandIds = selbst.Bandmitgliedschaften.Select(m => m.BandId).Distinct().ToList();
        if (bandIds.Count == 0) return null;   // Signal „gemeinsame Band" fehlt → kein Vorschlag

        var normSelf = Norm(selbst.Name);
        var kandidaten = await db.Personen
            .Where(p => p.BenutzerId == null && p.Id != selbst.Id
                && p.Bandmitgliedschaften.Any(m => bandIds.Contains(m.BandId)))
            .Select(p => new
            {
                p.Id, p.Name, p.Sichtbarkeit,
                Rollen = p.Rollen.Select(r => r.Rolle).ToList(),
                Bands = p.Bandmitgliedschaften.Select(m => m.Band.Name).Distinct().ToList()
            })
            .ToListAsync();

        var treffer = kandidaten.FirstOrDefault(k => Norm(k.Name) == normSelf);
        if (treffer == null) return null;

        var sichtbar = treffer.Sichtbarkeit == Sichtbarkeit.Oeffentlich
            || treffer.Rollen.Any(r => r is PersonRolleTyp.Komponist or PersonRolleTyp.Dirigent);
        return new Kandidat(treffer.Id, treffer.Name, string.Join(", ", treffer.Bands), sichtbar);
    }

    /// <summary>Nimmt den Vorschlag an: verschmilzt die Selbst-Person in die erfasste Person
    /// (Verknüpfung wandert mit). Nur für NICHT-sichtbare Rollen (weicher Merge). Sichtbare Rollen
    /// laufen über das Gate/Admin (siehe UI).</summary>
    public static async Task<(bool Ok, string Meldung)> ZusammenfuehrenAsync(
        ApplicationDbContext db, string userId, Guid kandidatId)
    {
        var selbstId = await db.Personen.Where(p => p.BenutzerId == userId).Select(p => p.Id).FirstOrDefaultAsync();
        if (selbstId == Guid.Empty) return (false, "Keine verknüpfte Person.");
        return await PersonMergeService.MergeAsync(db, selbstId, kandidatId);
    }

    private static string Norm(string s)
        => new string((s ?? "").ToLowerInvariant().Where(c => !char.IsWhiteSpace(c)).ToArray());
}
