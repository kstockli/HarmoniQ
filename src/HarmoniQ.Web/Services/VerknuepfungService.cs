using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Verknüpft ein Benutzerkonto direkt mit einer Person („das bin ich").
///
/// VORÜBERGEHEND: Die Verknüpfung wird sofort **auto-bestätigt** (kein Admin-Schritt). Der
/// <see cref="PersonAnspruch"/> wird zur Nachvollziehbarkeit mit Status <c>Genehmigt</c> protokolliert.
/// Zum Reaktivieren der manuellen Prüfung: in den Aufrufern wieder einen Antrag mit Status
/// <c>Offen</c> anlegen (statt <see cref="DirektVerknuepfenAsync"/>) – die Admin-Queue
/// <c>/admin/verknuepfungen</c> bleibt dafür erhalten.
/// </summary>
public static class VerknuepfungService
{
    public static async Task<(bool Ok, string Meldung)> DirektVerknuepfenAsync(
        ApplicationDbContext db, Guid personId, string userId, string? begruendung)
    {
        // Konto darf nicht bereits mit einer anderen Person verknüpft sein (BenutzerId ist unique).
        if (await db.Personen.AnyAsync(p => p.BenutzerId == userId && p.Id != personId))
            return (false, "Dein Konto ist bereits mit einer anderen Person verknüpft.");

        var person = await db.Personen.FindAsync(personId);
        if (person == null) return (false, "Person nicht gefunden.");

        if (person.BenutzerId == userId)
            return (true, "Du bist bereits mit dieser Person verknüpft.");
        if (person.BenutzerId != null)
            return (false, "Diese Person ist bereits mit einem anderen Konto verknüpft.");

        person.BenutzerId = userId;
        db.PersonAnsprueche.Add(new PersonAnspruch
        {
            PersonId = personId,
            BenutzerId = userId,
            Begruendung = string.IsNullOrWhiteSpace(begruendung) ? null : begruendung.Trim(),
            Status = PersonAnspruchStatus.Genehmigt,
            EntschiedenAm = DateTime.UtcNow
        });

        // Evtl. weitere offene Anträge derselben Person hinfällig machen.
        var weitere = await db.PersonAnsprueche
            .Where(a => a.PersonId == personId && a.Status == PersonAnspruchStatus.Offen)
            .ToListAsync();
        foreach (var w in weitere) { w.Status = PersonAnspruchStatus.Abgelehnt; w.EntschiedenAm = DateTime.UtcNow; }

        await db.SaveChangesAsync();

        // E-Mail-Link der Person mit der Konto-E-Mail synchronisieren.
        var kontoEmail = await db.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync();
        await PersonLinkSync.SyncEmailAsync(db, userId, kontoEmail);

        return (true, "Mit deiner Person verknüpft.");
    }
}
