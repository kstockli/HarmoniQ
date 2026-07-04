using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// „Band folgen" (UX-Spec 4.2): verwaltet die private <see cref="BandInteresse"/>-Beziehung der
/// verknüpften Person eines Kontos. Getrennt von der <see cref="BandMitgliedschaft"/> (Roster) –
/// Folgen erscheint nirgends öffentlich und hat keinen Sichtbarkeits-Effekt.
/// </summary>
public static class BandFolgenService
{
    /// <summary>Die zum Konto verknüpfte Person (oder null, wenn nicht verknüpft).</summary>
    public static Task<Guid?> PersonIdAsync(ApplicationDbContext db, string? userId)
        => string.IsNullOrEmpty(userId)
            ? Task.FromResult<Guid?>(null)
            : db.Personen.Where(p => p.BenutzerId == userId).Select(p => (Guid?)p.Id).FirstOrDefaultAsync();

    /// <summary>Folgt die Person dieser Band bereits (explizit über <see cref="BandInteresse"/>)?</summary>
    public static Task<bool> FolgtAsync(ApplicationDbContext db, Guid personId, Guid bandId)
        => db.BandInteressen.AnyAsync(i => i.PersonId == personId && i.BandId == bandId);

    /// <summary>Ist die Person bereits (aktives oder ehemaliges) Mitglied der Band? Mitgliedschaft
    /// impliziert Folgen (Union), ein zusätzlicher Interesse-Eintrag ist dann unnötig.</summary>
    public static Task<bool> IstMitgliedAsync(ApplicationDbContext db, Guid personId, Guid bandId)
        => db.BandMitgliedschaften.AnyAsync(m => m.PersonId == personId && m.BandId == bandId);

    /// <summary>Schaltet den Folgen-Status um. Gibt den neuen Status zurück (true = folgt jetzt).</summary>
    public static async Task<bool> UmschaltenAsync(ApplicationDbContext db, Guid personId, Guid bandId)
    {
        var vorhanden = await db.BandInteressen
            .FirstOrDefaultAsync(i => i.PersonId == personId && i.BandId == bandId);
        if (vorhanden != null)
        {
            db.BandInteressen.Remove(vorhanden);
            await db.SaveChangesAsync();
            return false;
        }
        db.BandInteressen.Add(new BandInteresse { PersonId = personId, BandId = bandId });
        await db.SaveChangesAsync();
        return true;
    }
}
