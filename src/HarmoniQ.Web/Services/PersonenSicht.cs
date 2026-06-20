using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Viewer-abhängige Sichtbarkeit von Personen. Bandkolleg:innen (Personen, die mit der
/// eigenen verknüpften Person in mindestens einer Band sind) werden „voll“ angezeigt
/// (Name + Bild), unabhängig von deren eingestellter <see cref="Sichtbarkeit"/>.
/// Für alle anderen gilt die persönliche Sichtbarkeits-Einstellung; Bilder erscheinen
/// nur bei „Öffentlich“.
/// </summary>
public static class PersonenSicht
{
    /// <summary>
    /// Liefert die Person-Ids, die der eingeloggte Benutzer voll sehen darf:
    /// die eigene verknüpfte Person + alle Bandkolleg:innen. Leer, wenn nicht eingeloggt
    /// oder (noch) mit keiner Person verknüpft.
    /// </summary>
    public static async Task<HashSet<Guid>> LadeVollSichtbareAsync(ApplicationDbContext db, string? userId)
    {
        if (string.IsNullOrEmpty(userId)) return [];

        var meineId = await db.Personen
            .Where(p => p.BenutzerId == userId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();
        if (meineId == Guid.Empty) return [];

        var bandIds = await db.BandMitgliedschaften
            .Where(m => m.PersonId == meineId)
            .Select(m => m.BandId)
            .ToListAsync();

        var ids = await db.BandMitgliedschaften
            .Where(m => bandIds.Contains(m.BandId))
            .Select(m => m.PersonId)
            .ToHashSetAsync();

        // Bestätigte Freundschaften (beide Richtungen) → ebenfalls voll sichtbar.
        var freunde = await db.Freundschaften
            .Where(f => f.Status == FreundschaftStatus.Bestaetigt
                     && (f.AnfragerPersonId == meineId || f.EmpfaengerPersonId == meineId))
            .Select(f => f.AnfragerPersonId == meineId ? f.EmpfaengerPersonId : f.AnfragerPersonId)
            .ToListAsync();
        foreach (var f in freunde) ids.Add(f);

        ids.Add(meineId);
        return ids;
    }

    public static Sichtbarkeit Effektiv(Guid personId, Sichtbarkeit eigene, HashSet<Guid> vollSichtbar)
        => vollSichtbar.Contains(personId) ? Sichtbarkeit.Oeffentlich : eigene;

    public static string AnzeigeName(string name, Sichtbarkeit effektiv) => effektiv switch
    {
        Sichtbarkeit.Oeffentlich => name,
        Sichtbarkeit.NurInitialen => Initialen(name),
        _ => "?"
    };

    /// <summary>Bild nur bei effektiv „Öffentlich“.</summary>
    public static string? BildUrl(string? bildUrl, Sichtbarkeit effektiv)
        => effektiv == Sichtbarkeit.Oeffentlich ? bildUrl : null;

    private static string Initialen(string name)
    {
        var teile = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (teile.Length == 0) return "?";
        return string.Join(" ", teile.Select(t => char.ToUpperInvariant(t[0]) + "."));
    }
}
